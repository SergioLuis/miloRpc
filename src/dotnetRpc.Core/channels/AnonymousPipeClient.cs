using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Channels;

public class AnonymousPipeClient
{
    class EstablishedConnection
    {
        public ulong ConnectionId { get; }
        public AnonymousPipeClientStream ServerToClient { get; }
        public AnonymousPipeServerStream ClientToServer { get; }

        public EstablishedConnection(
            ulong connectionId,
            AnonymousPipeClientStream serverToClient,
            AnonymousPipeServerStream clientToServer)
        {
            ConnectionId = connectionId;
            ServerToClient = serverToClient;
            ClientToServer = clientToServer;
        }
    }

    class ReservedConnectionsPool : IDisposable
    {
        public class ReservedConnection
        {
            public AnonymousPipeClientStream ServerToClient { get; }
            public AnonymousPipeServerStream ClientToServer { get; }

            public ReservedConnection(
                AnonymousPipeClientStream serverToClient,
                AnonymousPipeServerStream clientToServer)
            {
                ServerToClient = serverToClient;
                ClientToServer = clientToServer;
            }
        }

        public ReservedConnectionsPool(AnonymousPipeFileCommPaths paths, int poolSize)
        {
            mLog = RpcLoggerFactory.CreateLogger("ReservedConnectionsPool");

            mPaths = paths;
            mPoolSize = poolSize;
            mReservedConnectionsQueue = new Queue<ulong>();
            mReservedConnectionsDict = new Dictionary<ulong, ReservedConnection>();
            mSyncLock = new object();

            mRequestedEventsQueue = new Queue<ulong>();
            mRequestedEventsLoopSyncLock = new object();

            mSemaphoreSlim = new SemaphoreSlim(0);

            Task.Factory.StartNew(ProcessConnectionRequestedEventLoop, TaskCreationOptions.LongRunning);
        }

        public void Dispose()
        {
            List<ulong> reservedConnectionIds;
            lock (mReservedConnectionsQueue)
            {
                reservedConnectionIds = mReservedConnectionsQueue.ToList();
                mReservedConnectionsQueue.Clear();
                mReservedConnectionsDict.Clear();
            }

            foreach (ulong reservedConnectionId in reservedConnectionIds)
            {
                try
                {
                    File.Delete(mPaths.BuildConnectionReservedFilePath(reservedConnectionId));
                }
                catch { } // Nothing to do
            }

            mSemaphoreSlim.Dispose();
            GC.SuppressFinalize(this);
        }

        void EnqueueConnectionRequestedEvent(ulong connectionId)
        {
            lock (mRequestedEventsLoopSyncLock)
            {
                mRequestedEventsQueue.Enqueue(connectionId);
                Monitor.Pulse(mRequestedEventsLoopSyncLock);
            }
        }

        async void ProcessConnectionRequestedEventLoop()
        {
            while (true)
            {
                ulong nextEvent;
                lock (mRequestedEventsLoopSyncLock)
                {
                    if (mRequestedEventsQueue.Count == 0)
                    {
                        Monitor.Wait(mRequestedEventsLoopSyncLock);
                        continue;
                    }

                    if (mRequestedEventsQueue.Count == 0)
                        return;

                    nextEvent = mRequestedEventsQueue.Dequeue();
                }

                try
                {
                    await ProcessFileSystemEvent(nextEvent);
                }
                catch (Exception ex)
                {
                    // TODO: Log the exception
                }
            }
        }

        async Task ProcessFileSystemEvent(ulong connectionId)
        {
            mLog.LogDebug(
                "Connection '{id}' requested, try to refill pool...",
                connectionId);

            await RefillPool();
        }

        internal async Task RefillPool()
        {
            lock (mReservedConnectionsQueue)
            {
                if (mReservedConnectionsQueue.Count >= mPoolSize)
                {
                    mLog.LogDebug(
                        "Pool still has {connectionsCount} connections, won't refill",
                        mReservedConnectionsQueue.Count);
                    return;
                }
            }

            mLog.LogDebug("Refilling pool");

            bool poolRefilled = false;
            while (!poolRefilled)
            {
                ulong nextConnectionId;
                while (true)
                {
                    nextConnectionId = mPaths.GetNextOfferedConnection();
                    if (nextConnectionId == AnonymousPipeFileCommPaths.INVALID_CONN_ID)
                    {
                        mLog.LogDebug("No new connection offered...");
                        await Task.Delay(100);
                        continue;
                    }

                    mLog.LogDebug(
                        "Trying to reserve connection '{id}'",
                        nextConnectionId);
                    break;
                }

                if (!mPaths.SetConnectionReserved(
                    nextConnectionId,
                    out string connReservedFilePath))
                {
                    mLog.LogDebug(
                        "Could not reserve connection '{id}'",
                        nextConnectionId);
                    continue;
                }

                mLog.LogDebug("Connection '{id}' reserved", nextConnectionId);

                AnonymousPipeClientStream serverToClient = new(
                    PipeDirection.In,
                    await File.ReadAllTextAsync(connReservedFilePath));

                AnonymousPipeServerStream clientToServer = new(
                    PipeDirection.Out,
                    HandleInheritability.Inheritable);

                await File.WriteAllTextAsync(
                    connReservedFilePath,
                    clientToServer.GetClientHandleAsString());

                // Wait for the file descriptor to settle in...
                await Task.Delay(100);

                ReservedConnection reservedConnection = new(serverToClient, clientToServer);

                lock (mSyncLock)
                {
                    mReservedConnectionsQueue.Enqueue(nextConnectionId);
                    mReservedConnectionsDict.Add(nextConnectionId, reservedConnection);

                    poolRefilled = mReservedConnectionsQueue.Count >= mPoolSize;
                }

                mSemaphoreSlim.Release(1);
            }

            mLog.LogDebug("Pool refilled");
        }

        public async Task RequestNextConnection(CancellationToken ct)
        {
            await mSemaphoreSlim.WaitAsync(ct);

            ulong nextConnectionId;
            lock (mSyncLock)
            {
                nextConnectionId = mReservedConnectionsQueue.Dequeue();
            }

            mPaths.SetConnectionRequested(nextConnectionId);
            EnqueueConnectionRequestedEvent(nextConnectionId);
        }

        public ReservedConnection? GetReservedConnection(ulong connectionId)
        {
            ReservedConnection? result;
            lock (mSyncLock)
            {
                if (!mReservedConnectionsDict.Remove(connectionId, out result))
                {
                    mLog.LogWarning(
                        "Connection '{id}' was not in the reserved pool",
                        connectionId);
                    return null;
                }
            }

            mLog.LogDebug(
                "Connection '{id}' returned and removed from the reserved pool",
                connectionId);
            return result;
        }

        readonly AnonymousPipeFileCommPaths mPaths;
        readonly int mPoolSize;

        readonly Queue<ulong> mReservedConnectionsQueue;
        readonly Dictionary<ulong, ReservedConnection> mReservedConnectionsDict;
        readonly object mSyncLock;

        readonly Queue<ulong> mRequestedEventsQueue;
        readonly object mRequestedEventsLoopSyncLock;

        readonly SemaphoreSlim mSemaphoreSlim;
        readonly ILogger mLog;
    }

    class EstablishedConnectionsQueue : IDisposable
    {
        public EstablishedConnectionsQueue(
            AnonymousPipeFileCommPaths paths,
            ReservedConnectionsPool reservedConnectionsPool)
        {
            mLog = RpcLoggerFactory.CreateLogger("EstablishedConnectionsQueue");

            mPaths = paths;
            mReservedConnectionsPool = reservedConnectionsPool;

            mEstablishedConnections = new Queue<EstablishedConnection>();
            mSyncLock = new object();
            mSemaphoreSlim = new SemaphoreSlim(0);

            mFsEventsQueue = new Queue<FileSystemEventArgs>();
            mFsEventsLoopSyncLock = new object();

            Task.Factory.StartNew(ProcessFileSystemEventLoop, TaskCreationOptions.LongRunning);
        }

        public void Dispose()
        {
            mSemaphoreSlim.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<EstablishedConnection?> GetNextEstablishedConnection(CancellationToken ct)
        {
            const int msTimeout = 1000;

            await mReservedConnectionsPool.RequestNextConnection(ct);

            while (!ct.IsCancellationRequested)
            {
                mLog.LogDebug("Waiting to dequeue a new established connection");
                if (await mSemaphoreSlim.WaitAsync(msTimeout, ct))
                {
                    lock (mSyncLock)
                    {
                        return mEstablishedConnections.Dequeue();
                    }
                }

                mLog.LogDebug(
                    "Waited {ms} ms. to establish a new connection without success, polling FS directly",
                    msTimeout);

                ulong nextEstablishedConnectionId = mPaths.GetNextEstablishedConnection();
                if (nextEstablishedConnectionId == AnonymousPipeFileCommPaths.INVALID_CONN_ID)
                {
                    mLog.LogDebug("Didn't found any new established connection polling FS");
                    continue;
                }

                mLog.LogDebug(
                    "Found connection '{id}' as established, will try to enqueue it",
                    nextEstablishedConnectionId);

                EnqueueEstablishedConnection(nextEstablishedConnectionId);
            }

            return null;
        }

        internal void OnCreated(object _, FileSystemEventArgs e)
            => EnqueueFileSystemEventArg(e);

        internal void OnRenamed(object _, FileSystemEventArgs e)
            => EnqueueFileSystemEventArg(e);

        void EnqueueFileSystemEventArg(FileSystemEventArgs e)
        {
            lock (mFsEventsLoopSyncLock)
            {
                mFsEventsQueue.Enqueue(e);
                Monitor.Pulse(mFsEventsLoopSyncLock);
            }
        }

        void ProcessFileSystemEventLoop()
        {
            while (true)
            {
                FileSystemEventArgs nextEvent;
                lock (mFsEventsLoopSyncLock)
                {
                    if (mFsEventsQueue.Count == 0)
                    {
                        Monitor.Wait(mFsEventsLoopSyncLock);
                        continue;
                    }

                    if (mFsEventsQueue.Count == 0)
                        return;

                    nextEvent = mFsEventsQueue.Dequeue();
                }

                try
                {
                    ProcessFileSystemEvent(nextEvent);
                }
                catch (Exception ex)
                {
                    // TODO: log the exception
                }
            }
        }

        void ProcessFileSystemEvent(FileSystemEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Name))
                return;

            ReadOnlySpan<char> name = e.Name.AsSpan();
            if (!mPaths.IsConnectionEstablishedFileName(name))
                return;

            EnqueueEstablishedConnection(mPaths.ParseConnectionId(name));
        }

        void EnqueueEstablishedConnection(ulong connectionId)
        {
            ReservedConnectionsPool.ReservedConnection? requestedConn =
                mReservedConnectionsPool.GetReservedConnection(connectionId);

            if (requestedConn == null)
            {
                mLog.LogWarning(
                    "Did not found connection '{id}' in the pool",
                    connectionId);
                return;
            }

            mLog.LogDebug(
                "Connection '{id}' established, enqueuing it",
                connectionId);

            EstablishedConnection result = new(
                connectionId,
                requestedConn.ServerToClient,
                requestedConn.ClientToServer);

            lock (mSyncLock)
            {
                mEstablishedConnections.Enqueue(result);
            }

            mLog.LogDebug("Releasing semaphore");
            int oldCount = mSemaphoreSlim.Release(1);
            mLog.LogTrace("Semaphore count: {count}", oldCount + 1);
        }

        readonly AnonymousPipeFileCommPaths mPaths;
        readonly ReservedConnectionsPool mReservedConnectionsPool;

        readonly Queue<EstablishedConnection> mEstablishedConnections;
        readonly object mSyncLock;

        readonly SemaphoreSlim mSemaphoreSlim;

        readonly Queue<FileSystemEventArgs> mFsEventsQueue;
        readonly object mFsEventsLoopSyncLock;

        readonly ILogger mLog;
    }

    public AnonymousPipeClient(
        string directory,
        string prefix = "",
        int reservedConnectionsPoolSize = 2)
    {
        mLog = RpcLoggerFactory.CreateLogger("AnonymousPipeClient");

        mPaths = new AnonymousPipeFileCommPaths(directory, prefix);
        mReservedConnectionsPool = new ReservedConnectionsPool(
            mPaths, reservedConnectionsPoolSize);
        mEstablishedConnectionsQueue = new EstablishedConnectionsQueue(
            mPaths, mReservedConnectionsPool);

        mFileSystemWatcher = new FileSystemWatcher(directory);
        mFileSystemWatcher.Filters.Add(
            mPaths.GetSearchPattern(AnonymousPipeFileCommPaths.FileExtensions.Requested));
        mFileSystemWatcher.Filters.Add(
            mPaths.GetSearchPattern(AnonymousPipeFileCommPaths.FileExtensions.Established));
        mFileSystemWatcher.InternalBufferSize = 64 * 1024;
    }

    public void Dispose()
    {
        mFileSystemWatcher.EnableRaisingEvents = false;

        mReservedConnectionsPool.Dispose();

        mFileSystemWatcher.Created -= mEstablishedConnectionsQueue.OnCreated;
        mFileSystemWatcher.Renamed -= mEstablishedConnectionsQueue.OnRenamed;
        mEstablishedConnectionsQueue.Dispose();

        mFileSystemWatcher.Dispose();
    }

    public async Task Start()
    {
        mFileSystemWatcher.EnableRaisingEvents = true;

        await mReservedConnectionsPool.RefillPool();

        mFileSystemWatcher.Created += mEstablishedConnectionsQueue.OnCreated;
        mFileSystemWatcher.Renamed += mEstablishedConnectionsQueue.OnRenamed;
    }

    public async Task<IRpcChannel> ConnectAsync()
        => await ConnectAsync(CancellationToken.None);

    public async Task<IRpcChannel> ConnectAsync(CancellationToken ct)
    {
        EstablishedConnection establishedConnection =
            await mEstablishedConnectionsQueue.GetNextEstablishedConnection(ct);

        IRpcChannel result = new AnonymousPipeRpcChannel(
            mPaths.BuildConnectionEstablishedFilePath(establishedConnection.ConnectionId),
            establishedConnection.ClientToServer,
            establishedConnection.ServerToClient);

        return result;
    }

    readonly AnonymousPipeFileCommPaths mPaths;
    readonly ReservedConnectionsPool mReservedConnectionsPool;
    readonly EstablishedConnectionsQueue mEstablishedConnectionsQueue;
    readonly FileSystemWatcher mFileSystemWatcher;

    readonly ILogger mLog;
}

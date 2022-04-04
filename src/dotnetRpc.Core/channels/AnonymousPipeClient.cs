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
            mLog = RpcLoggerFactory.CreateLogger("RequestedConnectionsPool");

            mPaths = paths;
            mPoolSize = poolSize;
            mReservedConnectionsQueue = new Queue<ulong>();
            mReservedConnectionsDict = new Dictionary<ulong, ReservedConnection>();
            mSyncLock = new object();

            mFsEventsQueue = new Queue<FileSystemEventArgs>();
            mFsEventsLoopSyncLock = new object();

            mSemaphoreSlim = new SemaphoreSlim(0);

            Task.Factory.StartNew(ProcessFileSystemEventLoop, TaskCreationOptions.LongRunning);
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

        // TODO: Remove FSW for this pool
        internal void OnCreated(object _, FileSystemEventArgs e)
            => EnqueueFileSystemEventArg(e);

        // TODO: Remove FSW for this pool
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

        async void ProcessFileSystemEventLoop()
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
                    await ProcessFileSystemEvent(nextEvent);
                }
                catch (Exception ex)
                {
                    // TODO: Log the exception
                }
            }
        }

        async Task ProcessFileSystemEvent(FileSystemEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Name))
                return;

            if (!mPaths.IsConnectionRequestedFileName(e.Name))
                return;

            ulong connectionId = mPaths.ParseConnectionId(e.Name);

            mLog.LogDebug(
                "Connection ID {0} requested, try to refill pool...",
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
                        "Pool still has {0} connections, won't refill",
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
                        "Trying to reserve connection {0}",
                        nextConnectionId);
                    break;
                }

                if (!mPaths.SetConnectionReserved(
                    nextConnectionId,
                    out string connReservedFilePath))
                {
                    mLog.LogDebug(
                        "Could not reserve connection {0}",
                        nextConnectionId);
                    continue;
                }

                mLog.LogDebug("Connection {0} successfully reserved!", nextConnectionId);

                AnonymousPipeClientStream serverToClient = new(
                    PipeDirection.In,
                    await File.ReadAllTextAsync(connReservedFilePath));

                AnonymousPipeServerStream clientToServer = new(
                    PipeDirection.Out,
                    HandleInheritability.Inheritable);

                await File.WriteAllTextAsync(
                    connReservedFilePath,
                    clientToServer.GetClientHandleAsString());

                ReservedConnection reservedConnection = new(serverToClient, clientToServer);

                lock (mSyncLock)
                {
                    mReservedConnectionsQueue.Enqueue(nextConnectionId);
                    mReservedConnectionsDict.Add(nextConnectionId, reservedConnection);

                    poolRefilled = mReservedConnectionsQueue.Count >= mPoolSize;
                }

                mSemaphoreSlim.Release(1);
            }
        }

        public async Task ReserveNextConnection(CancellationToken ct)
        {
            await mSemaphoreSlim.WaitAsync(ct);

            ulong nextConnectionId;
            lock (mSyncLock)
            {
                nextConnectionId = mReservedConnectionsQueue.Dequeue();
            }

            mPaths.SetConnectionRequested(nextConnectionId);
            // TODO: Push rename FS event
        }

        public ReservedConnection? GetReservedConnection(ulong connectionId)
        {
            lock (mSyncLock)
            {
                return mReservedConnectionsDict.Remove(
                    connectionId, out ReservedConnection? result)
                    ? result
                    : null;
            }
        }

        readonly AnonymousPipeFileCommPaths mPaths;
        readonly int mPoolSize;

        readonly Queue<ulong> mReservedConnectionsQueue;
        readonly Dictionary<ulong, ReservedConnection> mReservedConnectionsDict;
        readonly object mSyncLock;

        readonly Queue<FileSystemEventArgs> mFsEventsQueue;
        readonly object mFsEventsLoopSyncLock;

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
            await mReservedConnectionsPool.ReserveNextConnection(ct);

            while (!ct.IsCancellationRequested)
            {
                if (await mSemaphoreSlim.WaitAsync(100, ct))
                {
                    lock (mSyncLock)
                    {
                        return mEstablishedConnections.Dequeue();
                    }
                }

                // TODO: Poll the directory and try to find the next *.conn_established
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
                // More than one AnonymousPipeClient might be listening on
                // the same directory using the same prefix.
                return;
            }

            EstablishedConnection result = new(
                connectionId,
                requestedConn.ServerToClient,
                requestedConn.ClientToServer);

            lock (mSyncLock)
            {
                mEstablishedConnections.Enqueue(result);
            }

            mSemaphoreSlim.Release(1);
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

        mFileSystemWatcher.Created -= mReservedConnectionsPool.OnCreated;
        mFileSystemWatcher.Renamed -= mReservedConnectionsPool.OnRenamed;
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

        mFileSystemWatcher.Created += mReservedConnectionsPool.OnCreated;
        mFileSystemWatcher.Renamed += mReservedConnectionsPool.OnRenamed;

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

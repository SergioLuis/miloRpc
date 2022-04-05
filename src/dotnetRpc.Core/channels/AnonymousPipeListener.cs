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

public class AnonymousPipeListener : IDisposable
{
    class RequestedConnection
    {
        public ulong ConnectionId { get; }
        public AnonymousPipeServerStream ServerToClient { get; }
        public AnonymousPipeClientStream ClientToServer { get; }

        public RequestedConnection(
            ulong connectionId,
            AnonymousPipeServerStream serverToClient,
            AnonymousPipeClientStream clientToServer)
        {
            ConnectionId = connectionId;
            ServerToClient = serverToClient;
            ClientToServer = clientToServer;
        }
    }

    class OfferedConnectionsPool : IDisposable
    {
        public OfferedConnectionsPool(
            AnonymousPipeFileCommPaths paths,
            PoolSettings poolSettings)
        {
            mLog = RpcLoggerFactory.CreateLogger("OfferedConnectionsPool");

            mPaths = paths;
            mPoolSettings = poolSettings;

            mOfferedConnectionsCount = 0;
            mOfferedConnections = new Dictionary<ulong, AnonymousPipeServerStream>(
                poolSettings.LowerLimit + poolSettings.GrowthRate);
            mOfferedConnectionsSyncLock = new object();

            mFsEventsQueue = new Queue<FileSystemEventArgs>();
            mFsEventsLoopSyncLock = new object();

            mFsEventsLoopThread = new Thread(ProcessFileSystemEventLoop)
            {
                Name = "AnonymousPipeListener.OfferedConnectionsPool.ProcessFileSystemEventLoop",
                IsBackground = true
            };
        }

        public void Dispose()
        {
            Stop();

            List<ulong> offeredConnectionIds;
            lock (mOfferedConnections)
            {
                offeredConnectionIds = mOfferedConnections.Keys.ToList();
                mOfferedConnections.Clear();
                mOfferedConnectionsCount = 0;
            }

            foreach (ulong offeredConnectionId in offeredConnectionIds)
            {
                try
                {
                    File.Delete(mPaths.BuildConnectionOfferedFilePath(offeredConnectionId));
                }
                catch
                {
                    // ignored
                }
            }

            GC.SuppressFinalize(this);
        }

        internal async Task Start()
        {
            mFsEventsLoopThread.Start();
            await RefillPool();
        }

        void Stop()
        {
            if (mFsEventsLoopFinished)
                return;

            mFsEventsLoopFinished = true;
            lock (mFsEventsLoopSyncLock)
                Monitor.Pulse(mFsEventsLoopSyncLock);

            mFsEventsLoopThread.Join();
        }

        internal void OnCreated(object _, FileSystemEventArgs e)
            => EnqueueFileSystemEventArgs(e);

        internal void OnRenamed(object _, RenamedEventArgs e)
            => EnqueueFileSystemEventArgs(e);

        void EnqueueFileSystemEventArgs(FileSystemEventArgs e)
        {
            lock (mFsEventsLoopSyncLock)
            {
                mFsEventsQueue.Enqueue(e);
                Monitor.Pulse(mFsEventsLoopSyncLock);
            }
        }

        async void ProcessFileSystemEventLoop()
        {
            while (!mFsEventsLoopFinished)
            {
                FileSystemEventArgs nextEvent;
                lock (mFsEventsLoopSyncLock)
                {
                    if (mFsEventsQueue.Count == 0)
                    {
                        Monitor.Wait(mFsEventsLoopSyncLock);

                        if (mFsEventsLoopFinished)
                            break;

                        continue;
                    }

                    nextEvent = mFsEventsQueue.Dequeue();
                }

                if (string.IsNullOrEmpty(nextEvent.Name))
                    continue;

                if (!mPaths.IsConnectionReservedFileName(nextEvent.Name))
                    continue;

                ulong connectionId = mPaths.ParseConnectionId(nextEvent.Name);
                try
                {
                    await ProcessReservedConnection(connectionId);
                }
                catch (Exception ex)
                {
                    mLog.LogError(
                        "There was an error processing reserved connection '{id}': {message}",
                        connectionId, ex.Message);
                    mLog.LogDebug(
                        "StackTrace:{newLine}{stackTrace}",
                        Environment.NewLine, ex.StackTrace);
                }
            }

            mLog.LogTrace("ProcessFileSystemEventLoop thread finished");
        }

        async Task ProcessReservedConnection(ulong connectionId)
        {
            mLog.LogDebug("Connection '{id}' reserved", connectionId);

            lock (mOfferedConnectionsSyncLock)
            {
                mOfferedConnectionsCount -= 1;
            }

            await RefillPool();
        }

        async Task RefillPool()
        {
            lock (mOfferedConnectionsSyncLock)
            {
                if (mOfferedConnectionsCount >= mPoolSettings.LowerLimit)
                {
                    mLog.LogDebug(
                        "Pool still has {count} connections, won't refill",
                        mOfferedConnectionsCount);
                    return;
                }
            }

            mLog.LogDebug("Refilling pool");

            bool poolRefilled = false;
            int desiredPoolSize = mPoolSettings.LowerLimit + mPoolSettings.GrowthRate;

            while (!poolRefilled)
            {
                ulong nextConnectionId = Interlocked.Increment(ref mNextConnectionId);
                if (nextConnectionId == AnonymousPipeFileCommPaths.INVALID_CONN_ID)
                    nextConnectionId = Interlocked.Increment(ref mNextConnectionId);

                AnonymousPipeServerStream pipe = await BeginNewConnection(nextConnectionId);

                lock (mOfferedConnectionsSyncLock)
                {
                    mOfferedConnections.Add(nextConnectionId, pipe);
                    mOfferedConnectionsCount += 1;
                    poolRefilled = mOfferedConnectionsCount >= desiredPoolSize;
                }

                mPaths.SetConnectionOffered(nextConnectionId);
                mLog.LogDebug("Offered new connection '{id}'", nextConnectionId);
            }

            mLog.LogDebug("Pool refilled");
        }

        internal AnonymousPipeServerStream? GetConnection(ulong connectionId)
        {
            lock (mOfferedConnections)
            {
                if (mOfferedConnections.Remove(connectionId, out var result))
                {
                    mLog.LogDebug(
                        "Connection '{id}' returned and removed from the offered pool",
                        connectionId);
                    return result;
                }
            }

            mLog.LogWarning(
                "Connection '{id}' was not in the offered pool",
                connectionId);
            return null;
        }

        async Task<AnonymousPipeServerStream> BeginNewConnection(ulong connectionId)
        {
            AnonymousPipeServerStream serverToClient = new(
                PipeDirection.Out, HandleInheritability.Inheritable);

            string connBeginningFilePath = mPaths.BuildConnectionBeginningFilePath(connectionId);
            string clientHandle = serverToClient.GetClientHandleAsString();

            await File.WriteAllTextAsync(connBeginningFilePath, clientHandle);

            // Wait for the file descriptor to settle in...
            await Task.Delay(100);

            return serverToClient;
        }

        ulong mNextConnectionId;

        readonly PoolSettings mPoolSettings;
        readonly AnonymousPipeFileCommPaths mPaths;

        int mOfferedConnectionsCount;
        readonly Dictionary<ulong, AnonymousPipeServerStream> mOfferedConnections;
        readonly object mOfferedConnectionsSyncLock;

        readonly Queue<FileSystemEventArgs> mFsEventsQueue;
        readonly object mFsEventsLoopSyncLock;
        readonly Thread mFsEventsLoopThread;
        volatile bool mFsEventsLoopFinished;

        readonly ILogger mLog;
    }

    class RequestedConnectionsQueue : IDisposable
    {
        public RequestedConnectionsQueue(
            AnonymousPipeFileCommPaths paths,
            OfferedConnectionsPool pool)
        {
            mLog = RpcLoggerFactory.CreateLogger("RequestedConnectionsQueue");

            mPaths = paths;
            mPool = pool;

            mRequestedConnections = new Queue<RequestedConnection>();
            mSemaphoreSlim = new SemaphoreSlim(0);

            mFsEventsQueue = new Queue<FileSystemEventArgs>();
            mFsEventsLoopSyncLock = new object();

            mFsEventsLoopThread = new Thread(ProcessFileSystemEventLoop)
            {
                Name = "AnonymousPipeListener.RequestedConnectionsQueue.ProcessFileSystemEventLoop",
                IsBackground = true
            };
        }

        public void Dispose()
        {
            Stop();

            mSemaphoreSlim.Dispose();
            GC.SuppressFinalize(this);
        }

        internal void Start()
        {
            mFsEventsLoopThread.Start();
        }

        void Stop()
        {
            if (mFsEventsLoopFinished)
                return;

            mFsEventsLoopFinished = true;
            lock (mFsEventsLoopSyncLock)
                Monitor.Pulse(mFsEventsLoopSyncLock);

            mFsEventsLoopThread.Join();
        }

        public async Task<RequestedConnection> EstablishNextConnection(CancellationToken ct)
        {
            const int msTimeout = 1000;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                mLog.LogDebug("Waiting to dequeue a new established connection");
                if (await mSemaphoreSlim.WaitAsync(msTimeout, ct))
                {
                    RequestedConnection result;
                    lock (mRequestedConnections)
                    {
                        result = mRequestedConnections.Dequeue();
                    }

                    mPaths.SetConnectionEstablished(result.ConnectionId);
                    return result;
                }

                mLog.LogDebug(
                    "Waited {ms} ms. to obtain a new connection without success, polling FS directly",
                    msTimeout);

                ulong nextRequestedConnectionId = mPaths.GetNextRequestedConnection();
                if (nextRequestedConnectionId == AnonymousPipeFileCommPaths.INVALID_CONN_ID)
                {
                    mLog.LogDebug("Didn't found any new requested connection polling FS");
                    continue;
                }

                mLog.LogDebug(
                    "Found connection '{id}' as requested, will try to complete it",
                    nextRequestedConnectionId);

                CompleteAndEnqueueConnection(nextRequestedConnectionId);
            }
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

                        if (mFsEventsLoopFinished)
                            break;

                        continue;
                    }

                    nextEvent = mFsEventsQueue.Dequeue();
                }

                ReadOnlySpan<char> name = nextEvent.Name.AsSpan();
                if (!mPaths.IsConnectionRequestedFileName(name))
                    continue;

                ulong connectionId = mPaths.ParseConnectionId(name);
                try
                {
                    CompleteAndEnqueueConnection(connectionId);
                }
                catch (Exception ex)
                {
                    mLog.LogError(
                        "There was an error completing and enqueuing connection '{id}': {message}",
                        connectionId, ex.Message);
                    mLog.LogDebug(
                        "StackTrace:{newLine}{stackTrace}",
                        Environment.NewLine, ex.StackTrace);
                }
            }
        }

        void CompleteAndEnqueueConnection(ulong connectionId)
        {
            AnonymousPipeServerStream? serverToClient = mPool.GetConnection(connectionId);
            if (serverToClient == null)
            {
                mLog.LogWarning(
                    "Did not found connection '{id}' in the pool",
                    connectionId);
                return;
            }

            mLog.LogDebug(
                "Connection '{id}' requested, completing and enqueuing the connection",
                connectionId);

            AnonymousPipeClientStream clientToServer = new(
                PipeDirection.In,
                File.ReadAllText(mPaths.BuildConnectionRequestedFilePath(connectionId)));

            RequestedConnection requestedConnection =
                new(connectionId, serverToClient, clientToServer);

            lock (mRequestedConnections)
            {
                mRequestedConnections.Enqueue(requestedConnection);
            }

            mSemaphoreSlim.Release(1);
        }

        readonly AnonymousPipeFileCommPaths mPaths;
        readonly OfferedConnectionsPool mPool;

        readonly Queue<RequestedConnection> mRequestedConnections;
        readonly SemaphoreSlim mSemaphoreSlim;

        readonly Queue<FileSystemEventArgs> mFsEventsQueue;
        readonly object mFsEventsLoopSyncLock;
        readonly Thread mFsEventsLoopThread;
        volatile bool mFsEventsLoopFinished;

        readonly ILogger mLog;
    }

    public struct PoolSettings
    {
        public int LowerLimit;
        public int GrowthRate;
    }

    public AnonymousPipeListener(
        string directory,
        string prefix,
        PoolSettings poolSettings)
    {
        mLog = RpcLoggerFactory.CreateLogger("AnonymousPipeListener");

        mPaths = new AnonymousPipeFileCommPaths(directory, prefix);
        mOfferedConnectionsPool = new OfferedConnectionsPool(mPaths, poolSettings);
        mRequestedConnectionsQueue = new RequestedConnectionsQueue(
            mPaths, mOfferedConnectionsPool);

        mFileSystemWatcher = new FileSystemWatcher(directory);
        mFileSystemWatcher.Filters.Add(
            mPaths.GetSearchPattern(AnonymousPipeFileCommPaths.FileExtensions.Reserved));
        mFileSystemWatcher.Filters.Add(
            mPaths.GetSearchPattern(AnonymousPipeFileCommPaths.FileExtensions.Requested));
        mFileSystemWatcher.InternalBufferSize = 64 * 1024;
    }

    public void Dispose()
    {
        mFileSystemWatcher.EnableRaisingEvents = false;

        mFileSystemWatcher.Renamed -= mOfferedConnectionsPool.OnRenamed;
        mFileSystemWatcher.Created -= mOfferedConnectionsPool.OnCreated;
        mOfferedConnectionsPool.Dispose();

        mFileSystemWatcher.Renamed -= mRequestedConnectionsQueue.OnRenamed;
        mFileSystemWatcher.Created -= mRequestedConnectionsQueue.OnCreated;
        mRequestedConnectionsQueue.Dispose();

        mFileSystemWatcher.Dispose();
    }

    public async Task Start()
    {
        mLog.LogInformation(
            "AnonymousPipeListener starting on directory '{dir}', using prefix '{prefix}'",
            mPaths.BaseDirectory, mPaths.Prefix);

        await mOfferedConnectionsPool.Start();
        mRequestedConnectionsQueue.Start();

        mFileSystemWatcher.EnableRaisingEvents = true;

        mFileSystemWatcher.Renamed += mOfferedConnectionsPool.OnRenamed;
        mFileSystemWatcher.Created += mOfferedConnectionsPool.OnCreated;

        mFileSystemWatcher.Renamed += mRequestedConnectionsQueue.OnRenamed;
        mFileSystemWatcher.Created += mRequestedConnectionsQueue.OnCreated;
    }

    public async Task<IRpcChannel> AcceptPipeAsync()
        => await AcceptPipeAsync(CancellationToken.None);

    public async Task<IRpcChannel> AcceptPipeAsync(CancellationToken ct)
    {
        RequestedConnection requestedConnection =
            await mRequestedConnectionsQueue.EstablishNextConnection(ct);

        IRpcChannel result = new AnonymousPipeRpcChannel(
            mPaths.BuildConnectionEstablishedFilePath(requestedConnection.ConnectionId),
            requestedConnection.ServerToClient,
            requestedConnection.ClientToServer);

        return result;
    }

    readonly AnonymousPipeFileCommPaths mPaths;
    readonly OfferedConnectionsPool mOfferedConnectionsPool;
    readonly RequestedConnectionsQueue mRequestedConnectionsQueue;
    readonly FileSystemWatcher mFileSystemWatcher;

    readonly ILogger mLog;
}

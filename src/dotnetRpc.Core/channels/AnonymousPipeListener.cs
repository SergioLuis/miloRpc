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

            Task.Factory.StartNew(ProcessFileSystemEventLoop, TaskCreationOptions.LongRunning);
        }

        public void Dispose()
        {
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
                catch { } // Nothing to do
            }
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

            if (!mPaths.IsConnectionReservedFileName(e.Name))
                return;

            ulong connectionId = mPaths.ParseConnectionId(e.Name);

            Console.WriteLine(
                "OfferedConnectionsPool - Connection ID {0} reserved, try to refill pool...",
                connectionId);

            lock (mOfferedConnectionsSyncLock)
            {
                mOfferedConnectionsCount -= 1;
            }

            await RefillPool();
        }

        public async Task RefillPool()
        {
            lock (mOfferedConnectionsSyncLock)
            {
                if (mOfferedConnectionsCount >= mPoolSettings.LowerLimit)
                {
                    Console.WriteLine(
                        "AnonymousPipeListener - Pool still has {0} connections, won't refill",
                        mOfferedConnectionsCount);
                    return;
                }
            }

            Console.WriteLine("AnonymousPipeListener - Refilling pool");

            bool poolRefilled = false;
            while (!poolRefilled)
            {
                ulong nextConnectionId = Interlocked.Increment(ref mNextConnectionId);
                AnonymousPipeServerStream pipe = await BeginNewConnection(nextConnectionId);

                lock (mOfferedConnectionsSyncLock)
                {
                    mOfferedConnections.Add(nextConnectionId, pipe);
                    mOfferedConnectionsCount += 1;
                    poolRefilled = mOfferedConnectionsCount >= mPoolSettings.LowerLimit + mPoolSettings.GrowthRate;
                }

                mPaths.SetConnectionOffered(nextConnectionId);
                Console.WriteLine(
                    "AnonymousPipeListener - Offered connection {0}",
                    nextConnectionId);
            }

            Console.WriteLine("AnonymousPipeListener - Pool refilled");
        }

        public AnonymousPipeServerStream? GetConnection(ulong connectionId)
        {
            lock (mOfferedConnections)
            {
                return !mOfferedConnections.Remove(
                    connectionId, out AnonymousPipeServerStream? result)
                        ? null
                        : result;
            }
        }

        async Task<AnonymousPipeServerStream> BeginNewConnection(ulong connectionId)
        {
            AnonymousPipeServerStream serverToClient = new(
                PipeDirection.Out, HandleInheritability.Inheritable);

            string connBeginningFilePath = mPaths.BuildConnectionBeginningFilePath(connectionId);
            string clientHandle = serverToClient.GetClientHandleAsString();

            await File.WriteAllTextAsync(connBeginningFilePath, clientHandle);

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

            Task.Factory.StartNew(ProcessFileSystemEventLoop, TaskCreationOptions.LongRunning);
        }

        public void Dispose()
        {
            mSemaphoreSlim.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<RequestedConnection> EstablishNextConnection(CancellationToken ct)
        {
            await mSemaphoreSlim.WaitAsync(ct);

            RequestedConnection result;
            lock (mRequestedConnections)
            {
                result = mRequestedConnections.Dequeue();
            }

            mPaths.SetConnectionEstablished(result.ConnectionId);
            return result;
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
                    // TODO: Log the exception
                }
            }
        }

        void ProcessFileSystemEvent(FileSystemEventArgs e)
        {
            Console.WriteLine(
                "RequestedConnectionsQueue - OnCreated event triggered on file {0}",
                e.Name);

            if (string.IsNullOrEmpty(e.Name))
                return;

            ReadOnlySpan<char> name = e.Name.AsSpan();
            if (!mPaths.IsConnectionRequestedFileName(name))
                return;

            CompleteAndEnqueueConnection(
                mPaths.ParseConnectionId(name),
                File.ReadAllText(e.FullPath));
        }

        void CompleteAndEnqueueConnection(ulong connectionId, string clientToServerPipeHandle)
        {
            AnonymousPipeServerStream? serverToClient = mPool.GetConnection(connectionId);
            if (serverToClient == null)
                return;

            AnonymousPipeClientStream clientToServer =
                new(PipeDirection.In, clientToServerPipeHandle);

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
        mFileSystemWatcher.EnableRaisingEvents = true;

        await mOfferedConnectionsPool.RefillPool();

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

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

/**
 * AnonymousPipe channel flow (this class implements client-side behavior):
 *
 * 0) Server creates file [prefix]_[ID].conn_beginning
 *    - 'prefix' will be a string shared with the client. By filtering files
 *      by their prefix, several NamedPipeListener instances can work with the
 *      same directory
 *    - 'ID' is a rotatory unsigned long, auto-incremented with each connection
 *
 * 1) Server side creates an AnonymousPipeServerStream ServerToClient-Server (PipeDirection.Out)
 * 2) Server side writes the Pipe's ClientHandle in file [prefix]_[ID].conn_beginning
 * 3) Server side renames file [prefix]_[ID].conn_beginning to [prefix]_[ID].conn_offered
 *    - First creating the file, then renaming it prevents race conditions with
 *      the clients while writing the Pipe's ClientHandle
 *
 * 4) Client side scans for files starting with [prefix] and extension 'conn_offered'
 *    - Client sorts the files by their [ID]
 *    - Client will try to choose first the one with the lowest [ID]
 * 5) Client side renames file [prefix]_[ID].conn_offered to [prefix]_[ID].conn_requesting
 *    - Because a rename is an atomic operation, this prevents more than one
 *      thread/process requesting the same NamedPipe connection
 *      The first one that renames the file succeeds
 * 6) Client side reads ClientHandle from [prefix]_[ID].conn_requesting
 * 7) Client side creates an AnonymousPipeClientStream ServerToClient-Client (PipeDirection.In)
 *    with the ClientHandle read from [prefix]_[ID].conn_requesting
 * 7) Client side creates an AnonymousPipeServerStream ClientToServer-Client (PipeDirection.Out)
 * 8) Client side writes the Pipe's ClientHandle in file [prefix]_[ID].conn_requesting
 * 9) Client side renames file [prefix]_[ID].conn_requesting to [prefix]_[ID].conn_requested
 *
 * 10) Server side reads ClientHandle from [prefix]_[ID].conn_requested
 * 11) Server side creates an AnonymousPipeClientStream ClientToServer-Server (PipeDirection.In)
 *     with the ClientHandle read from [prefix]_[ID].conn_requested
 * 12) Server side renames file [prefix]_[ID].conn_requested to [prefix]_[ID].conn_established
 *
 * Now both parties are ready to communicate!
 */
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

            mSemaphoreSlim = new SemaphoreSlim(0);
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

        internal string GetWatcherFilter() => mPaths.GetSearchPattern(
            AnonymousPipeFileCommPaths.FileExtensions.Requested);

        internal async void OnRenamed(object _, RenamedEventArgs e)
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

        public async Task RefillPool()
        {
            lock (mReservedConnectionsQueue)
            {
                if (mReservedConnectionsQueue.Count >= mPoolSize)
                    return;
            }

            bool poolRefilled = false;
            while (!poolRefilled)
            {
                ulong nextConnectionId = 0;
                while (true)
                {
                    ulong? tmpNextConnectionId = mPaths.GetNextOfferedConnection();
                    if (!tmpNextConnectionId.HasValue)
                    {
                        await Task.Delay(100);
                        continue;
                    }

                    nextConnectionId = tmpNextConnectionId.Value;
                    break;
                }

                if (!mPaths.SetConnectionReserved(
                    nextConnectionId,
                    out string connReservedFilePath))
                {
                    continue;
                }

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

        public async Task<ulong> GetNextReservedConnectionId(CancellationToken ct)
        {
            await mSemaphoreSlim.WaitAsync(ct);

            lock (mSyncLock)
            {
                return mReservedConnectionsQueue.Dequeue();
            }
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
        }

        public void Dispose()
        {
            mSemaphoreSlim.Dispose();
            GC.SuppressFinalize(this);
        }

        internal string GetWatcherFilter()
            => mPaths.GetSearchPattern(AnonymousPipeFileCommPaths.FileExtensions.Established);

        public async Task<EstablishedConnection> GetNextEstablishedConnection(CancellationToken ct)
        {
            ulong nextConnectionId =
                await mReservedConnectionsPool.GetNextReservedConnectionId(ct);

            mPaths.SetConnectionRequested(nextConnectionId);

            await mSemaphoreSlim.WaitAsync(ct);

            EstablishedConnection result;
            lock (mSyncLock)
            {
                result = mEstablishedConnections.Dequeue();
            }

            return result;
        }

        internal void OnRenamed(object _, RenamedEventArgs e)
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
    }

    public void Dispose()
    {
        mFileSystemWatcher.EnableRaisingEvents = false;

        mFileSystemWatcher.Renamed -= mReservedConnectionsPool.OnRenamed;
        mReservedConnectionsPool.Dispose();

        mFileSystemWatcher.Renamed -= mEstablishedConnectionsQueue.OnRenamed;
        mEstablishedConnectionsQueue.Dispose();

        mFileSystemWatcher.Dispose();
    }

    public async Task Start()
    {
        mFileSystemWatcher.EnableRaisingEvents = true;

        await mReservedConnectionsPool.RefillPool();

        mFileSystemWatcher.Filters.Add(mReservedConnectionsPool.GetWatcherFilter());
        mFileSystemWatcher.Renamed += mReservedConnectionsPool.OnRenamed;

        mFileSystemWatcher.Filters.Add(mEstablishedConnectionsQueue.GetWatcherFilter());
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

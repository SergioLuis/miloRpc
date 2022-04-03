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
 * AnonymousPipe channel flow (this class implements server-side behavior):
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
        public OfferedConnectionsPool(AnonymousPipeFileCommPaths paths, int poolSize)
        {
            mLog = RpcLoggerFactory.CreateLogger("OfferedConnectionsPool");

            mPaths = paths;
            mPoolSize = poolSize;
            mOfferedConnections = new Dictionary<ulong, AnonymousPipeServerStream>(mPoolSize);
        }

        public void Dispose()
        {
            List<ulong> offeredConnectionIds;
            lock (mOfferedConnections)
            {
                offeredConnectionIds = mOfferedConnections.Keys.ToList();
                mOfferedConnections.Clear();
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

        internal string GetWatcherFilter() => mPaths.GetSearchPattern(
            AnonymousPipeFileCommPaths.FileExtensions.Reserved);

        internal async void OnRenamed(object _, RenamedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Name))
                return;

            if (!mPaths.IsConnectionReservedFileName(e.Name))
                return;

            ulong connectionId = mPaths.ParseConnectionId(e.Name);

            mLog.LogDebug(
                "Connection ID {0} reserved, try to refill pool...",
                connectionId);

            await RefillPool();
        }

        public async Task RefillPool()
        {
            lock (mOfferedConnections)
            {
                if (mOfferedConnections.Count >= mPoolSize * 1.5)
                {
                    mLog.LogDebug(
                        "Pool still has {0} connections, won't refill",
                        mOfferedConnections.Count);
                    return;
                }
            }

            mLog.LogDebug("Refilling pool");

            bool poolRefilled = false;
            while (!poolRefilled)
            {
                ulong nextConnectionId = Interlocked.Increment(ref mNextConnectionId);
                AnonymousPipeServerStream pipe = await BeginNewConnection(nextConnectionId);

                lock (mOfferedConnections)
                {
                    mOfferedConnections.Add(nextConnectionId, pipe);
                    poolRefilled = mOfferedConnections.Count >= mPoolSize * 1.5;
                }

                mPaths.SetConnectionOffered(nextConnectionId);
            }

            mLog.LogDebug("Pool refilled");
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

        readonly int mPoolSize;
        readonly AnonymousPipeFileCommPaths mPaths;
        readonly Dictionary<ulong, AnonymousPipeServerStream> mOfferedConnections;

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
        }

        public void Dispose()
        {
            mSemaphoreSlim.Dispose();
            GC.SuppressFinalize(this);
        }

        internal string GetWatcherFilter()
            => mPaths.GetSearchPattern(AnonymousPipeFileCommPaths.FileExtensions.Requested);

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

        internal void OnRenamed(object _, RenamedEventArgs e)
        {
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

        readonly ILogger mLog;
    }

    public AnonymousPipeListener(
        string directory,
        string prefix = "",
        int offeredConnectionsPoolSize = 5)
    {
        mLog = RpcLoggerFactory.CreateLogger("AnonymousPipeListener");

        mPaths = new AnonymousPipeFileCommPaths(directory, prefix);
        mOfferedConnectionsPool = new OfferedConnectionsPool(
            mPaths, offeredConnectionsPoolSize);
        mRequestedConnectionsQueue = new RequestedConnectionsQueue(
            mPaths, mOfferedConnectionsPool);

        mFileSystemWatcher = new FileSystemWatcher(directory);
    }

    public void Dispose()
    {
        mFileSystemWatcher.EnableRaisingEvents = false;

        mFileSystemWatcher.Renamed -= mOfferedConnectionsPool.OnRenamed;
        mOfferedConnectionsPool.Dispose();

        mFileSystemWatcher.Renamed -= mRequestedConnectionsQueue.OnRenamed;
        mRequestedConnectionsQueue.Dispose();

        mFileSystemWatcher.Dispose();
    }

    public async Task Start()
    {
        mFileSystemWatcher.EnableRaisingEvents = true;

        await mOfferedConnectionsPool.RefillPool();

        mFileSystemWatcher.Filters.Add(mOfferedConnectionsPool.GetWatcherFilter());
        mFileSystemWatcher.Renamed += mOfferedConnectionsPool.OnRenamed;

        mFileSystemWatcher.Filters.Add(mRequestedConnectionsQueue.GetWatcherFilter());
        mFileSystemWatcher.Renamed += mRequestedConnectionsQueue.OnRenamed;
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

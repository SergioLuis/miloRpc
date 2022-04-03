using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
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

    class RequestedConnectionsPool
    {
        internal class RequestedConnection
        {
            internal AnonymousPipeClientStream ServerToClient { get; }
            internal AnonymousPipeServerStream ClientToServer { get; }

            internal RequestedConnection(
                AnonymousPipeClientStream serverToClient,
                AnonymousPipeServerStream clientToServer)
            {
                ServerToClient = serverToClient;
                ClientToServer = clientToServer;
            }
        }

        public RequestedConnectionsPool(AnonymousPipeFileCommPaths paths, int poolSize)
        {
            mLog = RpcLoggerFactory.CreateLogger("RequestedConnectionsPool");

            mPaths = paths;
            mPoolSize = poolSize;
            mRequestedConnections = new Dictionary<ulong, RequestedConnection>();
        }

        public async Task RefillPool(CancellationToken ct)
        {
            lock (mRequestedConnections)
            {
                if (mRequestedConnections.Count >= mPoolSize)
                    return;
            }

            bool poolRefilled = false;
            while (!poolRefilled)
            {
                ulong nextConnectionId = 0;
                while (!ct.IsCancellationRequested)
                {
                    ulong? tmpNextConnectionId = mPaths.GetNextConnectionOffered();
                    if (!tmpNextConnectionId.HasValue)
                    {
                        await Task.Delay(100, ct);
                        continue;
                    }

                    nextConnectionId = tmpNextConnectionId.Value;
                    break;
                }

                if (!mPaths.TrySetConnectionAsRequesting(
                    nextConnectionId,
                    out string connRequestingFilePath))
                {
                    continue;
                }

                AnonymousPipeClientStream serverToClient = new(
                    PipeDirection.In,
                    await File.ReadAllTextAsync(connRequestingFilePath, ct));

                AnonymousPipeServerStream clientToServer = new(
                    PipeDirection.Out, HandleInheritability.Inheritable);

                await File.WriteAllTextAsync(
                    connRequestingFilePath,
                    clientToServer.GetClientHandleAsString(),
                    ct);

                RequestedConnection requestedConnection =
                    new(serverToClient, clientToServer);

                lock (mRequestedConnections)
                {
                    mRequestedConnections.Add(nextConnectionId, requestedConnection);
                    poolRefilled = mRequestedConnections.Count >= mPoolSize;
                }

                mPaths.SetConnectionAsRequested(nextConnectionId);
            }
        }

        public RequestedConnection? GetConnection(ulong connectionId)
        {
            lock (mRequestedConnections)
            {
                return !mRequestedConnections.Remove(
                    connectionId, out RequestedConnection? result)
                    ? null
                    : result;
            }
        }

        readonly AnonymousPipeFileCommPaths mPaths;
        readonly int mPoolSize;
        readonly Dictionary<ulong, RequestedConnection> mRequestedConnections;
        readonly ILogger mLog;
    }

    class EstablishedConnectionsQueue : IDisposable
    {
        public EstablishedConnectionsQueue(
            AnonymousPipeFileCommPaths paths,
            int establishedConnectionsQueueSize)
        {
            mLog = RpcLoggerFactory.CreateLogger("EstablishedConnectionsQueue");

            mPaths = paths;
            mEstablishedConnectionsQueueSize = establishedConnectionsQueueSize;
            mPool = new RequestedConnectionsPool(
                mPaths, mEstablishedConnectionsQueueSize * 2);

            mEstablishedConnections = new Queue<EstablishedConnection>();
            mSemaphoreSlim = new SemaphoreSlim(0);

            mFileSystemWatcher = mPaths.BuildWatcherMonitorEstablishedConns();
            mFileSystemWatcher.Renamed += OnFileRenamed;
            mFileSystemWatcher.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            mFileSystemWatcher.EnableRaisingEvents = false;
            mFileSystemWatcher.Renamed -= OnFileRenamed;
            mFileSystemWatcher.Dispose();
            mSemaphoreSlim.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<EstablishedConnection> EstablishNextConnection(CancellationToken ct)
        {
            await mPool.RefillPool(ct);
            await mSemaphoreSlim.WaitAsync(ct);

            EstablishedConnection result;
            lock (mEstablishedConnections)
            {
                result = mEstablishedConnections.Dequeue();
            }

            return result;
        }

        void OnFileRenamed(object _, RenamedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Name))
                return;

            ReadOnlySpan<char> name = e.Name.AsSpan();
            if (!mPaths.IsConnEstablishedFilePath(name))
                return;

            EnqueueConnection(mPaths.ParseConnectionId(name));
        }

        void EnqueueConnection(ulong connectionId)
        {
            RequestedConnectionsPool.RequestedConnection? requestedConn =
                mPool.GetConnection(connectionId);

            if (requestedConn == null)
                return;

            EstablishedConnection result = new(
                connectionId,
                requestedConn.ServerToClient,
                requestedConn.ClientToServer);

            lock (mEstablishedConnections)
            {
                mEstablishedConnections.Enqueue(result);
            }

            mSemaphoreSlim.Release(1);
        }

        bool MustRefillPool()
        {
            lock (mEstablishedConnections)
            {
                return mEstablishedConnections.Count < mEstablishedConnectionsQueueSize;
            }
        }

        readonly AnonymousPipeFileCommPaths mPaths;
        readonly int mEstablishedConnectionsQueueSize;
        readonly RequestedConnectionsPool mPool;

        readonly Queue<EstablishedConnection> mEstablishedConnections;
        readonly SemaphoreSlim mSemaphoreSlim;
        readonly FileSystemWatcher mFileSystemWatcher;

        readonly ILogger mLog;
    }

    public AnonymousPipeClient(
        string directory,
        string prefix = "",
        int establishedConnectionsQueueSize = 2)
    {
        mLog = RpcLoggerFactory.CreateLogger("AnonymousPipeClient");

        mPaths = new AnonymousPipeFileCommPaths(directory, prefix);
        mEstablishedConnectionsQueue = new EstablishedConnectionsQueue(
            mPaths, establishedConnectionsQueueSize);
    }

    public async Task<IRpcChannel> ConnectAsync()
        => await ConnectAsync(CancellationToken.None);

    public async Task<IRpcChannel> ConnectAsync(CancellationToken ct)
    {
        EstablishedConnection establishedConnection =
            await mEstablishedConnectionsQueue.EstablishNextConnection(ct);

        IRpcChannel result = new AnonymousPipeRpcChannel(
            mPaths.GetConnEstablishedFilePath(establishedConnection.ConnectionId),
            establishedConnection.ClientToServer,
            establishedConnection.ServerToClient);

        return result;
    }

    readonly AnonymousPipeFileCommPaths mPaths;
    readonly EstablishedConnectionsQueue mEstablishedConnectionsQueue;
    readonly ILogger mLog;
}

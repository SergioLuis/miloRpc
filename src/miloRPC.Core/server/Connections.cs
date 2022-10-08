using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using miloRPC.Core.Channels;
using miloRPC.Core.Shared;

namespace miloRPC.Core.Server;

public class Connections
{
    public class CancellableConnection : IComparable<CancellableConnection>
    {
        public ConnectionFromClient Connection { get; }
        public CancellationTokenSource CancellationTokenSource { get; }

        internal CancellableConnection(
            ConnectionFromClient connection,
            CancellationTokenSource cancellationTokenSource)
        {
            Connection = connection;
            CancellationTokenSource = cancellationTokenSource;
            mCachedConnectionId = connection.Context.Id;
        }

        public override int GetHashCode()
        {
            return (int)Connection.Context.Id;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            if (obj is not CancellableConnection other)
                return false;

            return other.mCachedConnectionId == mCachedConnectionId;
        }

        public int CompareTo(CancellableConnection? other)
        {
            if (other is null)
                return 1;

            if (mCachedConnectionId == other.mCachedConnectionId)
                return 0;

            if (mCachedConnectionId < other.mCachedConnectionId)
                return -1;

            return 1;
        }

        readonly uint mCachedConnectionId;
    }

    public RpcMetrics.RpcCounters Counters => mMetrics.Counters;

    public List<CancellableConnection> All
    {
        get
        {
            List<CancellableConnection> result;
            lock (mSyncLock)
                result = new List<CancellableConnection>(mAllConnections);

            result.Sort();
            return result;
        }
    }

    public Connections(
        StubCollection stubCollection,
        INegotiateRpcProtocol negotiateProtocol,
        IReadMethodId readMethodId,
        IWriteMethodCallResult writeMethodCallResult,
        ConnectionTimeouts connectionTimeouts,
        TimeSpan monitorFrequency)
    {
        mStubCollection = stubCollection;
        mNegotiateProtocol = negotiateProtocol;
        mReadMethodId = readMethodId;
        mWriteMethodCallResult = writeMethodCallResult;
        mConnectionTimeouts = connectionTimeouts;
        mMonitorFrequency = monitorFrequency;

        mLog = RpcLoggerFactory.CreateLogger("RunningConnections");
    }

    public async Task ForceConnectionRecollectAsync()
        => await RunConnectionCollectCycle(true, default);

    public async Task RunConnectionsMonitorLoopAsync(CancellationToken ct)
    {
        try
        {
            using PeriodicTimer pt = new(mMonitorFrequency);
            while (await pt.WaitForNextTickAsync(ct))
            {
                await RunConnectionCollectCycle(false, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    public void LaunchNewConnection(IRpcChannel rpcChannel, CancellationTokenSource cts)
    {
        ConnectionFromClient connFromClient = new(
            mStubCollection,
            mNegotiateProtocol,
            mReadMethodId,
            mWriteMethodCallResult,
            mMetrics,
            rpcChannel,
            mConnectionTimeouts);

        mLog.LogTrace(
            "New connection established. Id: {0}. From {1}. IdlingTimeout: {2} ms. RunningTimeout: {3} ms.",
            connFromClient.Context.Id,
            connFromClient.Context.RemoteEndPoint,
            mConnectionTimeouts.Idling,
            mConnectionTimeouts.Running);

        CancellableConnection cancellableConnection = new(connFromClient, cts);
        lock (mSyncLock)
        {
            mAllConnections.Add(cancellableConnection);
            mQueuedConnections.Add(connFromClient.Context.Id, cancellableConnection);
        }

        connFromClient.ConnectionActive += ConnectionFromClient_Active;
        connFromClient.ConnectionInactive += ConnectionFromClient_Inactive;

        ValueTask t = connFromClient.ProcessConnMessagesLoop(cts.Token);
        t.ConfigureAwait(false);
    }

    void ConnectionFromClient_Active(ConnectionFromClient sender)
    {
        sender.ConnectionActive -= ConnectionFromClient_Active;

        lock (mSyncLock)
        {
            if (!mQueuedConnections.Remove(sender.Context.Id, out CancellableConnection? conn))
            {
                mLog.LogError(
                    "Connection with ID {ConnectionId} is now active but was not queued before!",
                    sender.Context.Id);
                return;
            }
            
            mLog.LogInformation(
                "Connection {ConnectionId}: Queued -> Active",
                sender.Context.Id);

            mActiveConnections.Add(conn.Connection.Context.Id, conn);
        }
    }

    void ConnectionFromClient_Inactive(ConnectionFromClient sender)
    {
        sender.ConnectionInactive += ConnectionFromClient_Inactive;

        lock (mSyncLock)
        {
            if (!mActiveConnections.Remove(sender.Context.Id, out CancellableConnection? conn))
            {
                mLog.LogError(
                    "Connection {ConnectionId} is now inactive but was not active before!",
                    sender.Context.Id);
                return;
            }

            mLog.LogInformation(
                "Connection {ConnectionId]}: Active -> Inactive",
                sender.Context.Id);

            conn.CancellationTokenSource.Cancel();
            mInactiveConnectionsCurrentCycle.Add(conn.Connection.Context.Id, conn);
        }
    }

    async Task RunConnectionCollectCycle(bool forceEvictAllInactive, CancellationToken ct)
    {
        await mSemaphore.WaitAsync(ct);
        try
        {
            Dictionary<uint, CancellableConnection> activeConnections;
            lock (mSyncLock)
            {
                // An inactive connection lasts one monitor loop to be evicted.
                // Connections are not evicted immediately in the next loop because
                // the monitor might trigger too close to when the connection exited,
                // making it hard to monitor inactive connections
                //
                //  T1 - Connection 1 active -> inactive
                //  T2 - Monitor loop
                //  T3 - Connection 2 active -> inactive
                //  T4 - Monitor loop > Connection 1 evicted
                //  T5 - Monitor loop > Connection 2 evicted
                (mInactiveConnectionsCurrentCycle, mInactiveConnectionsPastCycle) =
                    (mInactiveConnectionsPastCycle, mInactiveConnectionsCurrentCycle);

                mLog.LogDebug(
                    "Evicting {InactiveConnectionCount} inactive connections from the system",
                    mInactiveConnectionsCurrentCycle.Count);

                mAllConnections.ExceptWith(mInactiveConnectionsCurrentCycle.Values);
                mInactiveConnectionsCurrentCycle.Clear();

                if (forceEvictAllInactive)
                {
                    mAllConnections.ExceptWith(mInactiveConnectionsPastCycle.Values);
                    mInactiveConnectionsPastCycle.Clear();
                }

                if (mActiveConnections.Count == 0)
                {
                    mLog.LogDebug("Identified 0 exited active connections on the system");
                    return;
                }

                activeConnections = new Dictionary<uint, CancellableConnection>(mActiveConnections);
            }

            int identified = 0;
            foreach (KeyValuePair<uint, CancellableConnection> activeConn in activeConnections)
            {
                ct.ThrowIfCancellationRequested();

                if (activeConn.Value.Connection.CurrentStatus == ConnectionFromClient.Status.Exited)
                {
                    // The connection exited on its own while we were examining them
                    continue;
                }

                if (activeConn.Value.Connection.IsConnected())
                {
                    // The connection's channel is still connected
                    continue;
                }

                mLog.LogDebug(
                    "Active connection {ConnectionId} not identified as exited but its socket is disconnected",
                    activeConn.Value.Connection.Context.Id);

                identified++;
                activeConn.Value.CancellationTokenSource.Cancel();
            }

            mLog.LogDebug(
                "Identified {ActiveConnectionCount} exited connections from the system",
                identified);
        }
        finally
        {
            mSemaphore.Release();
        }
    }

    readonly StubCollection mStubCollection;
    readonly INegotiateRpcProtocol mNegotiateProtocol;
    readonly IReadMethodId mReadMethodId;
    readonly IWriteMethodCallResult mWriteMethodCallResult;
    readonly ConnectionTimeouts mConnectionTimeouts;
    readonly TimeSpan mMonitorFrequency;

    readonly RpcMetrics mMetrics = new();

    readonly object mSyncLock = new();
    readonly SemaphoreSlim mSemaphore = new(1, 1);
    readonly Dictionary<uint, CancellableConnection> mQueuedConnections = new();
    readonly Dictionary<uint, CancellableConnection> mActiveConnections = new();
    Dictionary<uint, CancellableConnection> mInactiveConnectionsCurrentCycle = new();
    Dictionary<uint, CancellableConnection> mInactiveConnectionsPastCycle = new();
    readonly HashSet<CancellableConnection> mAllConnections = new();

    readonly ILogger mLog;
}

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using miloRPC.Core.Channels;
using miloRPC.Core.Shared;

namespace miloRPC.Core.Server;

public class ActiveConnections
{
    public class ActiveConnection
    {
        public ConnectionFromClient Conn { get; }
        public CancellationTokenSource Cts { get; }

        internal ActiveConnection(
            ConnectionFromClient conn, CancellationTokenSource cts)
        {
            Conn = conn;
            Cts = cts;
        }

        public override int GetHashCode()
        {
            return (int)Conn.ConnectionId;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            ActiveConnection? other = Unsafe.As<ActiveConnection>(obj);
            if (other is null)
                return false;

            return other.Conn.ConnectionId == this.Conn.ConnectionId;
        }
    }

    public RpcMetrics.RpcCounters Counters => mMetrics.Counters;

    public IReadOnlyCollection<ActiveConnection> Connections
    {
        get
        {
            lock (mActiveConnections)
                return new List<ActiveConnection>(mActiveConnections);
        }
    }

    internal ActiveConnections(
        StubCollection stubCollection,
        INegotiateRpcProtocol negotiateProtocol,
        IReadMethodId readMethodId,
        IWriteMethodCallResult writeMethodCallResult,
        ConnectionTimeouts connectionTimeouts)
    {
        mStubCollection = stubCollection;
        mNegotiateProtocol = negotiateProtocol;
        mReadMethodId = readMethodId;
        mWriteMethodCallResult = writeMethodCallResult;
        mConnectionTimeouts = connectionTimeouts;
        mMonitorLoop = new RecurringTask(MonitorActiveConnections, "MonitorActiveConnections");

        mLog = RpcLoggerFactory.CreateLogger("RunningConnections");
    }

    internal void StartConnectionMonitor(TimeSpan loopWaitTime, CancellationToken ct)
        => mMonitorLoop.Start(loopWaitTime, ct);

    internal async Task StopConnectionMonitorAsync()
        => await mMonitorLoop.StopAsync();

    public async Task ForceConnectionRecollectAsync()
        => await mMonitorLoop.FireEarlyAsync();

    internal void LaunchNewConnection(IRpcChannel rpcChannel, CancellationTokenSource cts)
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
            connFromClient.ConnectionId,
            connFromClient.RemoteEndPoint,
            mConnectionTimeouts.Idling,
            mConnectionTimeouts.Running);

        AddConnection(new ActiveConnection(connFromClient, cts));

        connFromClient.ProcessConnMessagesLoop(cts.Token).ConfigureAwait(false);
    }

    void AddConnection(ActiveConnection activeConn)
    {
        lock (mActiveConnections)
        {
            mActiveConnections.Add(activeConn);
            mLog.LogTrace(
                "Active connections after new one: {0}",
                mActiveConnections.Count);
        }
    }

    async Task MonitorActiveConnections(CancellationToken ct)
    {
        await Task.Yield();

        if (ct.IsCancellationRequested)
            return;

        HashSet<ActiveConnection> connections;
        lock (mActiveConnections)
        {
            if (mActiveConnections.Count == 0)
                return;

            connections = new HashSet<ActiveConnection>(mActiveConnections);
        }

        List<ActiveConnection> connsToRemove = new();
        foreach (ActiveConnection activeConn in connections)
        {
            if (ct.IsCancellationRequested)
                break;

            ConnectionFromClient connFromClient = activeConn.Conn;
            if (connFromClient.CurrentStatus == ConnectionFromClient.Status.Exited)
            {
                mLog.LogDebug(
                    "Connection {0} identified as exited and queued for removal",
                    connFromClient.ConnectionId);

                activeConn.Cts.Cancel();
                connsToRemove.Add(activeConn);
            }

            if (!connFromClient.IsConnected())
            {
                mLog.LogDebug(
                    "Connection {0} not identified as exited but its socket is disconnected",
                    connFromClient.ConnectionId);

                activeConn.Cts.Cancel();
            }
        }

        mLog.LogDebug(
            "Going to evict {0} exited connections from the system",
            connsToRemove.Count);

        lock (mActiveConnections)
        {
            mActiveConnections.ExceptWith(connsToRemove);
            mLog.LogTrace(
                "Active connections after eviction: {0}",
                mActiveConnections.Count);
        }

        mLog.LogTrace("MonitorActiveConnectionsLoop completed");
    }

    readonly StubCollection mStubCollection;
    readonly INegotiateRpcProtocol mNegotiateProtocol;
    readonly IReadMethodId mReadMethodId;
    readonly IWriteMethodCallResult mWriteMethodCallResult;
    readonly ConnectionTimeouts mConnectionTimeouts;
    readonly RecurringTask mMonitorLoop;
    readonly RpcMetrics mMetrics = new();
    readonly HashSet<ActiveConnection> mActiveConnections = new();
    readonly ILogger mLog;
}
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
        public ConnectionFromClient Connection { get; }
        public CancellationTokenSource Cts { get; }

        internal ActiveConnection(ConnectionFromClient connection, CancellationTokenSource cts)
        {
            Connection = connection;
            Cts = cts;
        }

        public override int GetHashCode()
        {
            return (int)Connection.Context.Id;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            ActiveConnection? other = Unsafe.As<ActiveConnection>(obj);
            if (other is null)
                return false;

            return other.Connection.Context.Id
                   == this.Connection.Context.Id;
        }
    }

    public RpcMetrics.RpcCounters Counters => mMetrics.Counters;

    public IReadOnlyList<ActiveConnection> Connections
    {
        get
        {
            lock (mActiveConnections)
                return new List<ActiveConnection>(mActiveConnections);
        }
    }

    public ActiveConnections(
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

    public void StartConnectionMonitor(TimeSpan loopWaitTime, CancellationToken ct)
        => mMonitorLoop.Start(loopWaitTime, ct);

    public async Task StopConnectionMonitorAsync()
        => await mMonitorLoop.StopAsync();

    public async Task ForceConnectionRecollectAsync()
        => await mMonitorLoop.FireEarlyAsync();

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

    void MonitorActiveConnections(CancellationToken ct)
    {
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

            ConnectionFromClient connFromClient = activeConn.Connection;
            if (connFromClient.CurrentStatus == ConnectionFromClient.Status.Exited)
            {
                mLog.LogDebug(
                    "Connection {0} identified as exited and queued for removal",
                    connFromClient.Context.Id);

                activeConn.Cts.Cancel();
                connsToRemove.Add(activeConn);
            }

            if (connFromClient.IsConnected())
                continue;

            mLog.LogDebug(
                "Connection {ConnectionId} not identified as exited but its socket is disconnected",
                connFromClient.Context.Id);

            activeConn.Cts.Cancel();
        }

        mLog.LogDebug(
            "Going to evict {ConnectionCount} exited connections from the system",
            connsToRemove.Count);

        lock (mActiveConnections)
        {
            mActiveConnections.ExceptWith(connsToRemove);
            mLog.LogTrace(
                "Active connections after eviction: {ConnectionCount}",
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

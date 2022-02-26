using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Server;

public class ActiveConnections
{
    class ActiveConnection
    {
        internal ConnectionFromClient Conn { get; private set; }
        internal CancellationTokenSource Cts { get; private set; }

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

            ActiveConnection? other = obj as ActiveConnection;
            if (other == null)
                return false;

            return other.Conn.ConnectionId == this.Conn.ConnectionId;
        }
    }

    public RpcMetrics.RpcCounters Counters { get => mMetrics.Counters; }

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
        mLog = RpcLoggerFactory.CreateLogger("RunningConnections");
    }

    internal Task MonitorConnectionsAsync(TimeSpan loopWaitTime, CancellationToken ct)
    {
        mbIsMonitorLoopRunning = true;
        return Task.Factory.StartNew(
            MonitorActiveConnectionsLoop,
            new LoopParams(loopWaitTime, ct),
            TaskCreationOptions.LongRunning).Unwrap();
    }

    internal void LaunchNewConnection(Socket socket, CancellationToken ct)
    {
        if (!mbIsMonitorLoopRunning)
            throw new InvalidOperationException("The MonitorConnections loop is not running!");

        CancellationTokenSource cts =
            CancellationTokenSource.CreateLinkedTokenSource(ct);

        RpcSocket rpcSocket = new(socket, cts.Token);
        ConnectionFromClient connFromClient = new(
            mStubCollection,
            mNegotiateProtocol,
            mReadMethodId,
            mWriteMethodCallResult,
            mMetrics,
            rpcSocket,
            mConnectionTimeouts);

        mLog.LogTrace(
            "New connection stablished. Id: {0}. From {1}. IdlingTimeout: {2} ms. RunningTimeout: {3} ms.",
            connFromClient.ConnectionId,
            connFromClient.RemoteEndPoint,
            mConnectionTimeouts.Idling,
            mConnectionTimeouts.Running);

        AddConnection(new(connFromClient, cts));

        connFromClient.ProcessConnMessagesLoop(ct).ConfigureAwait(false);
    }

    void AddConnection(ActiveConnection activeConn)
    {
        lock (mActiveConnections)
        {
            mActiveConnections.Add(activeConn);
            mLog.LogTrace(
                "Active connections after new one: {0}",
                mActiveConnections.Count);
            Monitor.Pulse(mActiveConnections);
        }
    }

    async Task MonitorActiveConnectionsLoop(object? state)
    {
        LoopParams? loopParams = state as LoopParams;
        if (loopParams == null)
            throw new InvalidOperationException();

        TimeSpan loopWaitTime = loopParams.LoopWaitTime;
        CancellationToken ct = loopParams.CancellationToken;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                HashSet<ActiveConnection> connections;
                lock (mActiveConnections)
                {
                    if (mActiveConnections.Count == 0)
                        Monitor.Wait(mActiveConnections, loopWaitTime);

                    if (ct.IsCancellationRequested)
                        break;

                    if (mActiveConnections.Count == 0)
                        continue;

                    connections = new(mActiveConnections);
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

                    if (!connFromClient.IsRpcSocketConnected())
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

                await Task.Delay(loopWaitTime, ct);
            }
        }
        catch (OperationCanceledException) { }

        mLog.LogTrace("MonitorActiveConnectionsLoop completed");
    }

    volatile bool mbIsMonitorLoopRunning = false;
    readonly StubCollection mStubCollection;
    readonly INegotiateRpcProtocol mNegotiateProtocol;
    readonly IReadMethodId mReadMethodId;
    readonly IWriteMethodCallResult mWriteMethodCallResult;
    readonly ConnectionTimeouts mConnectionTimeouts;
    readonly RpcMetrics mMetrics = new();
    readonly HashSet<ActiveConnection> mActiveConnections = new();
    readonly ILogger mLog;

    class LoopParams
    {
        internal TimeSpan LoopWaitTime { get; private set; }
        internal CancellationToken CancellationToken { get; private set; }

        internal LoopParams(TimeSpan loopWaitTime, CancellationToken ct)
        {
            LoopWaitTime = loopWaitTime;
            CancellationToken = ct;
        }
    }
}

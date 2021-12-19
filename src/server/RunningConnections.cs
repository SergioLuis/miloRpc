using System;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;

using dotnetRpc.Shared;

namespace dotnetRpc.Server;

internal class RunningConnections
{
    public int ConnIdleTimeoutMillis { get; set; } = Timeout.Infinite;

    public RunningConnections(RpcMetrics metrics, int initialConnIdleTimeoutMillis)
    {
        mMetrics = metrics;
        ConnIdleTimeoutMillis = initialConnIdleTimeoutMillis;
        mLog = RpcLoggerFactory.CreateLogger("RunningConnections");
    }

    public void EnqueueNewConnection(Socket socket, CancellationToken ct)
    {
        if (ConnIdleTimeoutMillis == Timeout.Infinite)
        {
            LaunchNoMonitor(socket, ct);
            return;
        }

        LaunchMonitor(socket, ct);
    }

    void LaunchNoMonitor(Socket socket, CancellationToken ct)
    {
        // The server does not have a timeout configured to purge idle connectiosn
        // We launch the task in a "fire and forget" fashion
        RpcSocket rpcSocket = new(socket, ct);
        mLog.LogTrace("New connection stablished from {0}", rpcSocket.RemoteEndPoint);

        ConnectionFromClient connFromClient = new(mMetrics, rpcSocket);

        connFromClient.ProcessConnMessagesLoop(ct).ConfigureAwait(false);
    }

    void LaunchMonitor(Socket socket, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    readonly RpcMetrics mMetrics;
    readonly object mSyncLock = new();
    readonly ILogger mLog;
}

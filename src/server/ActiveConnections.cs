using System;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;

using dotnetRpc.Shared;

namespace dotnetRpc.Server;

public class ActiveConnections
{
    public int ConnIdleTimeoutMillis { get; set; }
    public int ConnRunTimeoutMillis { get; set; }

    internal ActiveConnections(
        int initialConnIdleTimeoutMillis = Timeout.Infinite,
        int initialConnRunTimeoutMillis = Timeout.Infinite)
    {
        mMetrics = new();
        ConnIdleTimeoutMillis = initialConnIdleTimeoutMillis;
        ConnRunTimeoutMillis = initialConnRunTimeoutMillis;
        mLog = RpcLoggerFactory.CreateLogger("RunningConnections");
    }

    internal void EnqueueNewConnection(Socket socket, CancellationToken ct)
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
        mLog.LogTrace(
            "New connection stablished from {0}. IdleTimeout: {1} ms. RunTimeout: {2} ms.",
            rpcSocket.RemoteEndPoint,
            ConnIdleTimeoutMillis,
            ConnRunTimeoutMillis);

        ConnectionFromClient connFromClient = new(
            mMetrics,
            rpcSocket,
            ConnIdleTimeoutMillis,
            ConnRunTimeoutMillis);

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

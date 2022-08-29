using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using miloRPC.Core.Channels;
using miloRPC.Core.Server;
using miloRPC.Core.Shared;

namespace miloRPC.Channels.Quic;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("macOS")]
public class QuicServer : IServer<IPEndPoint>
{
    public event EventHandler<AcceptLoopStartEventArgs>? AcceptLoopStart;
    public event EventHandler<AcceptLoopStopEventArgs>? AcceptLoopStop;
    public event EventHandler<ConnectionAcceptEventArgs<IPEndPoint>>? ConnectionAccept;

    string IServer<IPEndPoint>.ServerProtocol => WellKnownProtocols.QUIC;
    IPEndPoint? IServer<IPEndPoint>.BindAddress => mBindAddress;
    ActiveConnections IServer<IPEndPoint>.ActiveConnections => mActiveConnections;
    ConnectionTimeouts IServer<IPEndPoint>.ConnectionTimeouts => mConnectionTimeouts;

    public QuicServer(
        IPEndPoint bindTo,
        StubCollection stubCollection) : this(
             bindTo,
             stubCollection,
             DefaultQuicServerProtocolNegotiation.Instance) { }

    public QuicServer(
        IPEndPoint bindTo,
        StubCollection stubCollection,
        INegotiateServerQuicRpcProtocol negotiateProtocol) : this(
            bindTo,
            stubCollection,
            negotiateProtocol,
            DefaultReadMethodId.Instance,
            DefaultWriteMethodCallResult.Instance) { }

    public QuicServer(
        IPEndPoint bindTo,
        StubCollection stubCollection,
        INegotiateServerQuicRpcProtocol negotiateProtocol,
        IReadMethodId readMethodId,
        IWriteMethodCallResult writeMethodCallResult) : this(
            bindTo,
            stubCollection,
            negotiateProtocol,
            readMethodId,
            writeMethodCallResult,
            ConnectionTimeouts.Default) { }

    public QuicServer(
        IPEndPoint bindTo,
        StubCollection stubCollection,
        INegotiateServerQuicRpcProtocol negotiateProtocol,
        IReadMethodId readMethodId,
        IWriteMethodCallResult writeMethodCallResult,
        ConnectionTimeouts connectionTimeouts)
    {
        mBindEndpoint = bindTo;
        mConnectionTimeouts = connectionTimeouts;
        mNegotiateProtocol = negotiateProtocol;
        mActiveConnections = new ActiveConnections(
            stubCollection,
            mNegotiateProtocol,
            readMethodId,
            writeMethodCallResult,
            connectionTimeouts);
        mLog = RpcLoggerFactory.CreateLogger("QuicServer");
    }

    Task IServer<IPEndPoint>.ListenAsync(CancellationToken ct)
        => Task.Factory.StartNew(AcceptLoop, ct, TaskCreationOptions.LongRunning).Unwrap();

    async Task AcceptLoop(object? state)
    {
        CancellationToken ct = (CancellationToken) state!;

        SslServerAuthenticationOptions sslOptions = new()
        {
            AllowRenegotiation = true,
            ApplicationProtocols = new List<SslApplicationProtocol>(
                mNegotiateProtocol.ApplicationProtocols),
            ServerCertificate = mNegotiateProtocol.ServerCertificate
        };

        QuicListener quicListener = new(mBindEndpoint, sslOptions);
        mBindAddress = quicListener.ListenEndPoint;

        ct.Register(() =>
        {
            mLog.LogTrace("Cancellation requested, stopping QuicListener");
            quicListener.Dispose();
            mLog.LogTrace("QuicListener stopped");
        });

        int launchCount = 0;
        mActiveConnections.StartConnectionMonitor(TimeSpan.FromSeconds(30), ct);
        while (true)
        {
            AcceptLoopStartEventArgs startArgs = new(launchCount++);
            AcceptLoopStart?.Invoke(this, startArgs);
            if (startArgs.CancelRequested)
                break;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    QuicConnection conn = await quicListener.AcceptConnectionAsync(ct);

                    ConnectionAcceptEventArgs<IPEndPoint> connAcceptArgs =
                        new(Unsafe.As<IPEndPoint>(conn.RemoteEndPoint));
                    ConnectionAccept?.Invoke(this, connAcceptArgs);
                    if (connAcceptArgs.CancelRequested)
                    {
                        await conn.CloseAsync(0x02, ct);
                        conn.Dispose();
                        continue;
                    }

                    CancellationTokenSource connCts =
                        CancellationTokenSource.CreateLinkedTokenSource(ct);
                    IRpcChannel rpcChannel = QuicRpcChannel.CreateForServer(conn, connCts.Token);

                    mActiveConnections.LaunchNewConnection(rpcChannel, connCts);
                }

                break;
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested)
                    break;

                // TODO: Log the exception
            }
        }

        AcceptLoopStopEventArgs endArgs = new(launchCount);
        AcceptLoopStop?.Invoke(this, endArgs);

        await mActiveConnections.StopConnectionMonitorAsync();

        mLog.LogTrace("AcceptLoop completed");
    }

    IPEndPoint? mBindAddress;
    readonly ActiveConnections mActiveConnections;
    readonly ConnectionTimeouts mConnectionTimeouts;
    readonly INegotiateServerQuicRpcProtocol mNegotiateProtocol;
    readonly IPEndPoint mBindEndpoint;
    readonly ILogger mLog;
}

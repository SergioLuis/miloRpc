using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using miloRPC.Core.Channels;
using miloRPC.Core.Extensions;
using miloRPC.Core.Shared;

namespace miloRPC.Core.Server;

public class TcpServer : IServer
{
    public event EventHandler<AcceptLoopStartEventArgs>? AcceptLoopStart;
    public event EventHandler<AcceptLoopStopEventArgs>? AcceptLoopStop;
    public event EventHandler<ConnectionAcceptEventArgs>? ConnectionAccept;

    IPEndPoint? IServer.BindAddress => mBindAddress;
    ActiveConnections IServer.ActiveConnections => mActiveConns;
    ConnectionTimeouts IServer.ConnectionTimeouts => mConnectionTimeouts;

    public TcpServer(
        IPEndPoint bindTo,
        StubCollection stubCollection) : this(
            bindTo,
            stubCollection,
            DefaultServerProtocolNegotiation.Instance,
            DefaultReadMethodId.Instance,
            DefaultWriteMethodCallResult.Instance) { }

    public TcpServer(
        IPEndPoint bindTo,
        StubCollection stubCollection,
        INegotiateRpcProtocol negotiateProtocol) : this(
            bindTo,
            stubCollection,
            negotiateProtocol,
            DefaultReadMethodId.Instance,
            DefaultWriteMethodCallResult.Instance) { }

    public TcpServer(
        IPEndPoint bindTo,
        StubCollection stubCollection,
        INegotiateRpcProtocol negotiateProtocol,
        IReadMethodId readMethodId,
        IWriteMethodCallResult writeMethodCallResult)
        : this(
            bindTo,
            stubCollection,
            negotiateProtocol,
            readMethodId,
            writeMethodCallResult,
            ConnectionTimeouts.AllInfinite) { }

    public TcpServer(
        IPEndPoint bindTo,
        StubCollection stubCollection,
        INegotiateRpcProtocol negotiateProtocol,
        IReadMethodId readMethodId,
        IWriteMethodCallResult writeMethodCallResult,
        ConnectionTimeouts connectionTimeouts)
    {
        mBindEndpoint = bindTo;
        mConnectionTimeouts = connectionTimeouts;
        mActiveConns = new ActiveConnections(
            stubCollection,
            negotiateProtocol,
            readMethodId,
            writeMethodCallResult,
            connectionTimeouts);
        mLog = RpcLoggerFactory.CreateLogger("TcpServer");
    }

    Task IServer.ListenAsync(CancellationToken ct)
        => Task.Factory.StartNew(AcceptLoop, ct, TaskCreationOptions.LongRunning).Unwrap();

    async Task AcceptLoop(object? state)
    {
        CancellationToken ct = (CancellationToken)state!;
        TcpListener tcpListener = new(mBindEndpoint);
        tcpListener.Start();

        mBindAddress = Unsafe.As<IPEndPoint>(tcpListener.LocalEndpoint);

        ct.Register(() =>
        {
            mLog.LogTrace("Cancellation requested, stopping TcpListener");
            tcpListener.Stop();
            mLog.LogTrace("TCP listener stopped");
        });

        int launchCount = 0;
        mActiveConns.StartConnectionMonitor(TimeSpan.FromSeconds(30), ct);
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
                    Socket socket = await tcpListener.AcceptSocketAsync(ct);

                    ConnectionAcceptEventArgs connAcceptArgs = new(socket.RemoteEndPoint);
                    ConnectionAccept?.Invoke(this, connAcceptArgs);
                    if (connAcceptArgs.CancelRequested)
                    {
                        socket.ShutdownAndCloseSafely();
                        continue;
                    }

                    CancellationTokenSource connCts =
                        CancellationTokenSource.CreateLinkedTokenSource(ct);
                    IRpcChannel rpcChannel = new TcpRpcChannel(socket, connCts.Token);

                    mActiveConns.LaunchNewConnection(rpcChannel, connCts);
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

        await mActiveConns.StopConnectionMonitorAsync();

        mLog.LogTrace("AcceptLoop completed");
    }

    IPEndPoint? mBindAddress;
    readonly ActiveConnections mActiveConns;
    readonly ConnectionTimeouts mConnectionTimeouts;
    readonly IPEndPoint mBindEndpoint;
    readonly ILogger mLog;
}

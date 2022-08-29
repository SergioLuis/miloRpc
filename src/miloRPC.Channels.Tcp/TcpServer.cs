using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using miloRPC.Core.Channels;
using miloRPC.Core.Extensions;
using miloRPC.Core.Server;
using miloRPC.Core.Shared;

namespace miloRPC.Channels.Tcp;

public class TcpServer : IServer<IPEndPoint>
{
    public event EventHandler<AcceptLoopStartEventArgs>? AcceptLoopStart;
    public event EventHandler<AcceptLoopStopEventArgs>? AcceptLoopStop;
    public event EventHandler<ConnectionAcceptEventArgs<IPEndPoint>>? ConnectionAccept;

    string IServer<IPEndPoint>.ServerProtocol => WellKnownProtocols.TCP;
    IPEndPoint? IServer<IPEndPoint>.BindAddress => mBindAddress;
    ActiveConnections IServer<IPEndPoint>.ActiveConnections => mActiveConnections;
    ConnectionTimeouts IServer<IPEndPoint>.ConnectionTimeouts => mConnectionTimeouts;

    public TcpServer(
        IPEndPoint bindTo,
        StubCollection stubCollection) : this(
            bindTo,
            stubCollection,
            DefaultServerProtocolNegotiation.Instance) { }

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
            ConnectionTimeouts.Default) { }

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
        mActiveConnections = new ActiveConnections(
            stubCollection,
            negotiateProtocol,
            readMethodId,
            writeMethodCallResult,
            connectionTimeouts);
        mLog = RpcLoggerFactory.CreateLogger("TcpServer");
    }

    Task IServer<IPEndPoint>.ListenAsync(CancellationToken ct)
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
            mLog.LogTrace("TcpListener stopped");
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
                    Socket socket = await tcpListener.AcceptSocketAsync(ct);

                    ConnectionAcceptEventArgs<IPEndPoint> connAcceptArgs =
                        new(Unsafe.As<IPEndPoint>(socket.RemoteEndPoint));
                    ConnectionAccept?.Invoke(this, connAcceptArgs);
                    if (connAcceptArgs.CancelRequested)
                    {
                        ShutdownSocket(socket);
                        continue;
                    }

                    CancellationTokenSource connCts =
                        CancellationTokenSource.CreateLinkedTokenSource(ct);
                    IRpcChannel rpcChannel = new TcpRpcChannel(socket, connCts.Token);

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

    static void ShutdownSocket(Socket socket)
    {
        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        catch
        {
            // Nothing to do
        }
        finally
        {
            socket.Close();
        }
    }

    IPEndPoint? mBindAddress;
    readonly ActiveConnections mActiveConnections;
    readonly ConnectionTimeouts mConnectionTimeouts;
    readonly IPEndPoint mBindEndpoint;
    readonly ILogger mLog;
}

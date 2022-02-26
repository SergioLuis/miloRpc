using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Server;

public interface IServer
{
    ActiveConnections ActiveConnections { get; }
    ConnectionTimeouts ConnectionTimeouts { get; }
    Task ListenAsync(CancellationToken ct);
}

public class TcpServer : IServer
{
    public ActiveConnections ActiveConnections { get => mActiveConns; }
    public ConnectionTimeouts ConnectionTimeouts { get => mConnectionTimeouts; }

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
        mActiveConns = new(
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

        ct.Register(() =>
        {
            mLog.LogTrace("Cancellation requested, stopping TcpListener");
            tcpListener.Stop();
            mLog.LogTrace("TCP listener stopped");
        });

        Task activeConnsMonitorLoop =
            mActiveConns.MonitorConnectionsAsync(TimeSpan.FromSeconds(30), ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                Socket socket = await tcpListener.AcceptSocketAsync(ct);
                // TODO: Maybe this socket needs some settings, define and apply them
                mActiveConns.LaunchNewConnection(socket, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (SocketException ex)
        {
            // TODO: Handle the exception
            throw;
        }
        catch (Exception ex)
        {
            // TODO: Handle the exception
            throw;
        }

        await activeConnsMonitorLoop;

        mLog.LogTrace("AcceptLoop completed");
    }

    readonly ActiveConnections mActiveConns;
    readonly ConnectionTimeouts mConnectionTimeouts;
    readonly IPEndPoint mBindEndpoint;
    readonly ILogger mLog;
}

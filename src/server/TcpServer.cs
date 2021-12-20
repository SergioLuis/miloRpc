using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Shared;

namespace dotnetRpc.Server;

public interface IServer
{
    Task ListenAsync(CancellationToken ct);
}

public class TcpServer : IServer
{
    public ActiveConnections ActiveConnections { get => mActiveConns; }

    public TcpServer(
        IPEndPoint bindTo,
        int initialConnIdleTimeoutMillis = Timeout.Infinite,
        int initialConnRunTimeoutMillis = Timeout.Infinite)
    {
        mBindEndpoint = bindTo;
        mActiveConns = new(
            initialConnIdleTimeoutMillis,
            initialConnRunTimeoutMillis);
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

        while (!ct.IsCancellationRequested)
        {
            try
            {
                Socket socket = await tcpListener.AcceptSocketAsync(ct);
                // TODO: Maybe this socket needs some settings, define and apply them
                mActiveConns.EnqueueNewConnection(socket, ct);
            }
            catch (OperationCanceledException ex)
            {
                // The server is exiting - nothing to do for now
            }
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
        }

        mLog.LogTrace("AcceptLoop completed");
    }

    readonly ActiveConnections mActiveConns;
    readonly IPEndPoint mBindEndpoint;
    readonly ILogger mLog;
}

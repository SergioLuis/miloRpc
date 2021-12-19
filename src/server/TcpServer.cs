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
    public RpcMetrics.RpcCounters MetricCounters => mMetrics.Counters;

    public TcpServer(IPEndPoint bindTo)
    {
        mMetrics = new();
        mBindEndpoint = bindTo;
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

                CancellationTokenSource connCts =
                    CancellationTokenSource.CreateLinkedTokenSource(ct);

                RpcSocket rpcSocket = new(socket, connCts.Token);
                ConnectionFromClient connectionFromClient = new(mMetrics, rpcSocket);

                mLog.LogTrace("New connection stablished from {0}", rpcSocket.RemoteEndPoint);

                // TODO: Maybe this socket needs some settings, define and apply them

                // FIXME: this blocks new accepts until the call finishes
                await connectionFromClient.StartProcessingMessages(connCts.Token);
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
    }

    readonly RpcMetrics mMetrics;
    readonly IPEndPoint mBindEndpoint;
    readonly ILogger mLog;
}

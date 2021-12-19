using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace dotnetRpc.Server;

public interface IServer
{
    Task ListenAsync(CancellationToken ct);
}

public class TcpServer : IServer
{
    public TcpServer(IPEndPoint bindTo)
    {
        mBindEndpoint = bindTo;
    }

    Task IServer.ListenAsync(CancellationToken ct)
        => Task.Factory.StartNew(AcceptLoop, ct, TaskCreationOptions.LongRunning).Unwrap();

    async Task AcceptLoop(object? state)
    {
        CancellationToken ct = (CancellationToken)state!;
        TcpListener tcpListener = new(mBindEndpoint);
        tcpListener.Start();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                Socket socket = await tcpListener.AcceptSocketAsync(ct);
                RpcSocket rpcSocket = new(socket);
                ConnectionFromClient connectionFromClient = new(rpcSocket);

                // TODO: Maybe this socket needs some settings, define and apply them

                // FIXME: this blocks new accepts until the call finishes
                await connectionFromClient.StartProcessingMessages(ct);
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

    readonly IPEndPoint mBindEndpoint;
}

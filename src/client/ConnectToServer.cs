using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace dotnetRpc.Client;

public class ConnectToServer
{
    public ConnectToServer(IPEndPoint connectTo)
    {
        mServerEndpoint = connectTo;
    }

    public async Task<ConnectionToServer> ConnectAsync(CancellationToken ct)
    {
        TcpClient tcpClient = new();
        await tcpClient.ConnectAsync(mServerEndpoint, ct);
        return new(tcpClient);
    }

    readonly IPEndPoint mServerEndpoint;
}

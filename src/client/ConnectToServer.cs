using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using dotnetRpc.Extensions;

namespace dotnetRpc.Client;

public class ConnectToServer
{
    public ConnectToServer(IPEndPoint connectTo)
    {
        mServerEndpoint = connectTo;
    }

    public async Task<ConnectionToServer> ConnectAsync(int connectionTimeout, CancellationToken ct)
    {
        TcpClient tcpClient = new();
        await tcpClient.ConnectAsync(mServerEndpoint, connectionTimeout, ct);
        return new(tcpClient);
    }

    readonly IPEndPoint mServerEndpoint;
}

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using dotnetRpc.Extensions;
using dotnetRpc.Shared;

namespace dotnetRpc.Client;

public class ConnectToServer
{
    public ConnectToServer(IPEndPoint connectTo, INegotiateRpcProtocol negotiateProtocol)
    {
        mServerEndpoint = connectTo;
        mNegotiateProtocol = negotiateProtocol;
    }

    public async Task<ConnectionToServer> ConnectAsync(int connectionTimeout, CancellationToken ct)
    {
        TcpClient tcpClient = new();
        await tcpClient.ConnectAsync(mServerEndpoint, connectionTimeout, ct);
        return new(tcpClient, mNegotiateProtocol);
    }

    readonly IPEndPoint mServerEndpoint;
    readonly INegotiateRpcProtocol mNegotiateProtocol;
}

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

    public async Task<TcpClient> ConnectAsync(CancellationToken ct)
    {
        TcpClient result = new();
        await result.ConnectAsync(mServerEndpoint, ct);
        return result;
    }

    readonly IPEndPoint mServerEndpoint;
}

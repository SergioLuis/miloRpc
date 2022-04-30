using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using miloRPC.Core.Channels;
using miloRPC.Core.Shared;

namespace miloRPC.Core.Client;

public class ConnectToServer
{
    public ConnectToServer(IPEndPoint connectTo) : this(
        connectTo,
        DefaultClientProtocolNegotiation.Instance,
        DefaultWriteMethodId.Instance,
        DefaultReadMethodCallResult.Instance) { }

    public ConnectToServer(
        IPEndPoint connectTo,
        INegotiateRpcProtocol negotiateProtocol) : this(
            connectTo,
            negotiateProtocol,
            DefaultWriteMethodId.Instance,
            DefaultReadMethodCallResult.Instance) { }

    public ConnectToServer(
        IPEndPoint connectTo,
        INegotiateRpcProtocol negotiateProtocol,
        IWriteMethodId writeMethodId,
        IReadMethodCallResult readMethodCallResult)
    {
        mServerEndpoint = connectTo;
        mNegotiateProtocol = negotiateProtocol;
        mWriteMethodId = writeMethodId;
        mReadMethodCallResult = readMethodCallResult;
        mMetrics = new RpcMetrics();
    }

    public async Task<ConnectionToServer> ConnectAsync(CancellationToken ct)
    {
        TcpClient tcpClient = new();
        await tcpClient.ConnectAsync(mServerEndpoint, ct);

        TcpRpcChannel channel = new(tcpClient.Client, ct);
        return new ConnectionToServer(
            mNegotiateProtocol,
            mWriteMethodId,
            mReadMethodCallResult,
            mMetrics,
            channel);
    }

    readonly IPEndPoint mServerEndpoint;
    readonly INegotiateRpcProtocol mNegotiateProtocol;
    readonly IWriteMethodId mWriteMethodId;
    readonly IReadMethodCallResult mReadMethodCallResult;
    readonly RpcMetrics mMetrics;
}

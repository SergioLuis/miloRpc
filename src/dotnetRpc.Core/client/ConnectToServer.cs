using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using dotnetRpc.Core.Channels;
using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Client;

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
        mMetrics = new();
    }

    public async Task<ConnectionToServer> ConnectAsync(CancellationToken ct)
    {
        TcpClient tcpClient = new();
        await tcpClient.ConnectAsync(mServerEndpoint, ct);

        RpcTcpChannel tcpChannel = new(tcpClient.Client, ct);
        return new(
            mNegotiateProtocol,
            mWriteMethodId,
            mReadMethodCallResult,
            mMetrics,
            tcpChannel);
    }

    readonly IPEndPoint mServerEndpoint;
    readonly INegotiateRpcProtocol mNegotiateProtocol;
    readonly IWriteMethodId mWriteMethodId;
    readonly IReadMethodCallResult mReadMethodCallResult;
    readonly RpcMetrics mMetrics;
}

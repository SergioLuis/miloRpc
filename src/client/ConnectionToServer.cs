using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using dotnetRpc.Shared;

namespace dotnetRpc.Client;

public class ConnectionToServer
{
    public ConnectionToServer(
        INegotiateRpcProtocol negotiateProtocol,
        RpcMetrics clientMetrics,
        TcpClient tcpClient)
    {
        mNegotiateProtocol = negotiateProtocol;
        mClientMetrics = clientMetrics;
        mTcpClient = tcpClient;

        mConnectionId = mClientMetrics.ConnectionStart();
    }

    public async Task<string> InvokeEchoRequest(string echoRequest) // Temporary code to test changes
    {
        if (mRpc is null)
        {
            mRpc = await mNegotiateProtocol.NegotiateProtocolAsync(
                mConnectionId,
                Unsafe.As<IPEndPoint>(mTcpClient.Client.RemoteEndPoint)!,
                mTcpClient.GetStream());
        }

        mRpc.Writer.Write((byte)255); // Method ID for echo request
        mRpc.Writer.Write(echoRequest);
        mRpc.Writer.Flush();

        // Method executed, now wait for the reply
        string reply = mRpc.Reader.ReadString();

        return reply;
    }

    RpcProtocolNegotiationResult? mRpc;

    readonly uint mConnectionId;
    readonly RpcMetrics mClientMetrics;
    readonly TcpClient mTcpClient;
    readonly INegotiateRpcProtocol mNegotiateProtocol;
}

using System.Collections.Generic;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using miloRPC.Core.Channels;
using miloRPC.Core.Client;
using miloRPC.Core.Shared;

namespace miloRPC.Channels.Quic;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("macOS")]
public class ConnectToQuicServer : IConnectToServer
{
    public ConnectToQuicServer(IPEndPoint connectTo) : this(
        connectTo,
        DefaultQuicClientProtocolNegotiation.Instance,
        DefaultWriteMethodId.Instance,
        DefaultReadMethodCallResult.Instance) { }

    public ConnectToQuicServer(
        IPEndPoint connectTo,
        INegotiateClientQuicRpcProtocol negotiateProtocol) : this(
            connectTo,
            negotiateProtocol,
            DefaultWriteMethodId.Instance,
            DefaultReadMethodCallResult.Instance) { }

    public ConnectToQuicServer(
        IPEndPoint connectTo,
        INegotiateClientQuicRpcProtocol negotiateProtocol,
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
        SslClientAuthenticationOptions authOptions = new();
        authOptions.AllowRenegotiation = true;
        authOptions.ApplicationProtocols =
            new List<SslApplicationProtocol>(mNegotiateProtocol.ApplicationProtocols);

        if (mNegotiateProtocol.ValidateServerCertificate != null)
        {
            authOptions.RemoteCertificateValidationCallback =
                new RemoteCertificateValidationCallback(
                    mNegotiateProtocol.ValidateServerCertificate);
        }

        QuicConnection client = new(mServerEndpoint, authOptions);
        await client.ConnectAsync(ct);

        IRpcChannel channel = QuicRpcChannel.CreateForClient(client, ct);
        return new ConnectionToServer(
            mNegotiateProtocol,
            mWriteMethodId,
            mReadMethodCallResult,
            mMetrics,
            channel);
    }

    readonly IPEndPoint mServerEndpoint;
    readonly INegotiateClientQuicRpcProtocol mNegotiateProtocol;
    readonly IWriteMethodId mWriteMethodId;
    readonly IReadMethodCallResult mReadMethodCallResult;
    readonly RpcMetrics mMetrics;
}

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;

using miloRPC.Channels.Quic;
using miloRPC.Channels.Tcp;
using miloRPC.Core.Client;
using miloRPC.Core.Shared;

namespace miloRpc.TestWorkBench.Client;

public class MiloConnectionPools
{
    private MiloConnectionPools()
    {
        mPools = new Dictionary<string, Dictionary<IPEndPoint, ConnectionPool>>(
            StringComparer.InvariantCultureIgnoreCase);
        mSyncLock = new object();
    }
    
    public ConnectionPool Get(string protocol, IPEndPoint endpoint)
    {
        lock (mSyncLock)
        {
            ConnectionPool? result;
            if (mPools.TryGetValue(protocol, out var protocolPools))
            {
                if (protocolPools.TryGetValue(endpoint, out result))
                    return result;

                result = BuildPool(protocol, endpoint);
                protocolPools[endpoint] = result;
                return result;
            }

            protocolPools = new Dictionary<IPEndPoint, ConnectionPool>();
            mPools[protocol] = protocolPools;
            result = BuildPool(protocol, endpoint);
            protocolPools[endpoint] = result;
            return result;
        }
    }

    static ConnectionPool BuildPool(string protocol, IPEndPoint endPoint)
        => new(BuildConnectToServer(protocol, endPoint));

    static IConnectToServer BuildConnectToServer(string protocol, IPEndPoint endPoint)
        => protocol switch
        {
            "tcp" => BuildConnectToTcpServer(endPoint),
            "ssl" => BuildConnectToTcpSslServer(endPoint),
            "quic" => BuildConnectToQuicServer(endPoint),
            _ => throw new NotSupportedException($"Unknown protocol {protocol}")
        };

    static ConnectToTcpServer BuildConnectToTcpServer(IPEndPoint endPoint)
        => new(
            endPoint,
            new DefaultClientProtocolNegotiation(
                RpcCapabilities.None,
                RpcCapabilities.None));

    static ConnectToTcpServer BuildConnectToTcpSslServer(IPEndPoint endPoint)
    {
        ConnectionSettings connectionSettings = new()
        {
            Ssl = new ConnectionSettings.SslSettings
            {
                CertificateValidationCallback = ConnectionSettings.SslSettings.AcceptAllCertificates
            },
            Buffering = new ConnectionSettings.BufferingSettings
            {
                Enable = false
            }
        };

        return new ConnectToTcpServer(
            endPoint,
            new DefaultClientProtocolNegotiation(
                RpcCapabilities.Ssl,
                RpcCapabilities.None,
                connectionSettings,
                ArrayPool<byte>.Shared));
    }

    static ConnectToQuicServer BuildConnectToQuicServer(IPEndPoint endPoint)
        => new(
            endPoint,
            new DefaultQuicClientProtocolNegotiation(
                RpcCapabilities.None,
                RpcCapabilities.None,
                ArrayPool<byte>.Shared,
                new List<SslApplicationProtocol> {new("miloworkbench")},
                DefaultQuicClientProtocolNegotiation.AcceptAllCertificates));

    readonly Dictionary<string, Dictionary<IPEndPoint, ConnectionPool>> mPools;
    readonly object mSyncLock;

    public static readonly MiloConnectionPools Instance = new();
}

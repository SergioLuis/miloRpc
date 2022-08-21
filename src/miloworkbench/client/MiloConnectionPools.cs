using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Runtime.Versioning;

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
        => new(endPoint, new DefaultClientProtocolNegotiation(ConnectionSettings.None));

    static ConnectToTcpServer BuildConnectToTcpSslServer(IPEndPoint endPoint)
    {
        ConnectionSettings connectionSettings = new()
        {
            Ssl = new ConnectionSettings.SslSettings
            {
                Status = SharedCapabilityEnablement.EnabledMandatory,
                CertificateValidationCallback = ConnectionSettings.SslSettings.AcceptAllCertificates
            },
            Compression = ConnectionSettings.CompressionSettings.Disabled,
            Buffering = ConnectionSettings.BufferingSettings.Disabled
        };

        return new ConnectToTcpServer(
            endPoint, new DefaultClientProtocolNegotiation(connectionSettings));
    }

    static ConnectToQuicServer BuildConnectToQuicServer(IPEndPoint endPoint)
    {
        ConnectionSettings connectionSettings = new()
        {
            Ssl = new ConnectionSettings.SslSettings
            {
                Status = SharedCapabilityEnablement.EnabledMandatory,
                CertificateValidationCallback = ConnectionSettings.SslSettings.AcceptAllCertificates,
                ApplicationProtocols = new []
                {
                    new SslApplicationProtocol("miloworkbench")
                }
            },
            Compression = ConnectionSettings.CompressionSettings.Disabled,
            Buffering = ConnectionSettings.BufferingSettings.Disabled
        };

        return new ConnectToQuicServer(
            endPoint, new DefaultQuicClientProtocolNegotiation(connectionSettings));
    }

    readonly Dictionary<string, Dictionary<IPEndPoint, ConnectionPool>> mPools;
    readonly object mSyncLock;

    public static readonly MiloConnectionPools Instance = new();
}

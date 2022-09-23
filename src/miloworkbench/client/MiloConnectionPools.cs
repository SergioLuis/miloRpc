using System;
using System.Collections.Generic;
using System.Net;

using miloRPC.Channels.Quic;
using miloRPC.Channels.Tcp;
using miloRPC.Core.Client;
using miloRPC.Core.Shared;

namespace miloRpc.TestWorkBench.Client;

public class MiloConnectionPools
{
    public MiloConnectionPools(
        ConnectionSettings tcpConnectionSettings,
        ConnectionSettings sslConnectionSettings,
        ConnectionSettings quicConnectionSettings)
    {
        mTcpConnectionSettings = tcpConnectionSettings;
        mSslConnectionSettings = sslConnectionSettings;
        mQuicConnectionSettings = quicConnectionSettings;

        mPools = new Dictionary<string, Dictionary<IPEndPoint, ConnectionPool>>(
            StringComparer.InvariantCultureIgnoreCase);
        mSyncLock = new object();
    }
    
    public ConnectionPool Get(Uri uri)
    {
        string protocol = uri.Scheme;
        IPEndPoint endpoint = IPEndPoint.Parse($"{uri.Host}:{uri.Port}");

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

    ConnectionPool BuildPool(string protocol, IPEndPoint endPoint)
        => new(BuildConnectToServer(protocol, endPoint));

    IConnectToServer BuildConnectToServer(string protocol, IPEndPoint endPoint)
        => protocol switch
        {
            "tcp" => BuildConnectToTcpServer(endPoint, mTcpConnectionSettings),
            "ssl" => BuildConnectToTcpSslServer(endPoint, mSslConnectionSettings),
            "quic" => BuildConnectToQuicServer(endPoint, mQuicConnectionSettings),
            _ => throw new NotSupportedException($"Unknown protocol {protocol}")
        };

    static ConnectToTcpServer BuildConnectToTcpServer(
        IPEndPoint endPoint, ConnectionSettings connectionSettings)
    {
        return new ConnectToTcpServer(
            endPoint, new DefaultClientProtocolNegotiation(connectionSettings));
    }

    static ConnectToTcpServer BuildConnectToTcpSslServer(
        IPEndPoint endPoint, ConnectionSettings connectionSettings)
    {
        return new ConnectToTcpServer(
            endPoint, new DefaultClientProtocolNegotiation(connectionSettings));
    }

    static ConnectToQuicServer BuildConnectToQuicServer(
        IPEndPoint endPoint, ConnectionSettings connectionSettings)
    {
        return new ConnectToQuicServer(
            endPoint, new DefaultQuicClientProtocolNegotiation(connectionSettings));
    }

    readonly ConnectionSettings mTcpConnectionSettings;
    readonly ConnectionSettings mSslConnectionSettings;
    readonly ConnectionSettings mQuicConnectionSettings;

    readonly Dictionary<string, Dictionary<IPEndPoint, ConnectionPool>> mPools;
    readonly object mSyncLock;
}

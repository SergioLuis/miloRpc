using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using dotnetRpc.Shared;

namespace dotnetRpc.Client;

public class DefaultClientProtocolNegotiation : INegotiateRpcProtocol
{
    [Flags]
    public enum RpcCapabilities : byte
    {
        None            = 0,
        Ssl             = 1 << 0,
        Compression     = 1 << 1
    }

    [Flags]
    public enum CompressionFlags : byte
    {
        None            = 0,
        Brotli          = 1 << 0,
        GZip            = 1 << 1,
        ZLib            = 1 << 2
    }

    byte INegotiateRpcProtocol.CurrentProtocolVersion => CURRENT_VERSION;

    bool INegotiateRpcProtocol.CanHandleProtocolVersion(int version) => version == CURRENT_VERSION;

    public DefaultClientProtocolNegotiation()
    {
        
    }

    Task<RpcProtocolNegotiationResult> INegotiateRpcProtocol.NegotiateProtocolAsync(
        int version,
        IPEndPoint _,
        Stream baseStream,
        BinaryReader tempReader,
        BinaryWriter tempWriter)
    {
        if (version == 1)
        {
            return NegotiateProtocolV1Async(
                baseStream,
                tempReader,
                tempWriter);
        }

        throw new NotImplementedException();
    }

    Task<RpcProtocolNegotiationResult> NegotiateProtocolV1Async(
        Stream baseStream,
        BinaryReader tempReader,
        BinaryWriter tempWriter)
    {
        throw new NotImplementedException();
    }

    const byte CURRENT_VERSION = 1;
}

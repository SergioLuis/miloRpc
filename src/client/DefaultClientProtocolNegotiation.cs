using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Shared;
using RpcCapabilities = dotnetRpc.Shared.DefaultProtocolNegotiation.RpcCapabilities;
using Compression = dotnetRpc.Shared.DefaultProtocolNegotiation.Compression;

namespace dotnetRpc.Client;

public class DefaultClientProtocolNegotiation : INegotiateRpcProtocol
{
    byte INegotiateRpcProtocol.CurrentProtocolVersion => CURRENT_VERSION;

    bool INegotiateRpcProtocol.CanHandleProtocolVersion(int version) => version == CURRENT_VERSION;

    public DefaultClientProtocolNegotiation(
        RpcCapabilities mandatoryCapabilites,
        RpcCapabilities optionalCapabilities,
        Compression compressionFlags)
    {
        mLog = RpcLoggerFactory.CreateLogger("DefaultClientProtocolNegotiation");
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

    readonly ILogger mLog;
    const byte CURRENT_VERSION = 1;
}

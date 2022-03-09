using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Client;

public class DefaultClientProtocolNegotiation : INegotiateRpcProtocol
{
    public DefaultClientProtocolNegotiation(
        RpcCapabilities mandatoryCapabilities,
        RpcCapabilities optionalCapabilities,
        Compression compressionFlags)
    {
        mMandatoryCapabilities = mandatoryCapabilities;
        mOptionalCapabilities = optionalCapabilities;
        mCompressionFlags = compressionFlags;

        mLog = RpcLoggerFactory.CreateLogger("DefaultClientProtocolNegotiation");
    }

    Task<RpcProtocolNegotiationResult> INegotiateRpcProtocol.NegotiateProtocolAsync(
        uint connId,
        IPEndPoint remoteEndpoint,
        Stream baseStream)
    {
        BinaryReader tempReader = new(baseStream);
        BinaryWriter tempWriter = new(baseStream);

        tempWriter.Write((byte)CURRENT_VERSION);
        tempWriter.Flush();

        byte versionToUse = tempReader.ReadByte();

        if (versionToUse == 1)
        {
            return NegotiateProtocolV1Async(
                connId,
                remoteEndpoint,
                baseStream,
                tempReader,
                tempWriter);
        }

        throw new InvalidOperationException(
            $"Not prepared to negotiate protocol version {versionToUse}");
    }

    Task<RpcProtocolNegotiationResult> NegotiateProtocolV1Async(
        uint connId,
        IPEndPoint remoteEndPoint,
        Stream baseStream,
        BinaryReader tempReader,
        BinaryWriter tempWriter)
    {
        tempWriter.Write((byte)mMandatoryCapabilities);
        tempWriter.Write((byte)mOptionalCapabilities);
        tempWriter.Flush();

        RpcCapabilities serverMandatory = (RpcCapabilities)tempReader.ReadByte();
        RpcCapabilities serverOptional = (RpcCapabilities)tempReader.ReadByte();

        RpcCapabilitiesNegotiationResult negotiationResult =
            RpcCapabilitiesNegotiationResult.Build(
                mMandatoryCapabilities,
                mOptionalCapabilities,
                serverMandatory,
                serverOptional);

        if (!negotiationResult.NegotiatedOk)
        {
            string exMessage = string.Format(
                "Protocol was not correctly negotiated for conn {0}. "
                + "Required missing capabilities: {1}",
                connId,
                negotiationResult.RequiredMissingCapabilities);
            throw new NotSupportedException(exMessage);
        }

        if (negotiationResult.CommonCapabilities.HasFlag(RpcCapabilities.Ssl))
        {
            throw new NotSupportedException("Ssl is not supported yet");
        }

        if (negotiationResult.CommonCapabilities.HasFlag(RpcCapabilities.Compression))
        {
            throw new NotSupportedException("Compression is not supported yet");
        }

        mLog.LogInformation(
            "Protocol was correctly negotiated for conn {0}. " +
            "Optional missing capabilities: {1}",
            connId,
            negotiationResult.OptionalMissingCapabilities);

        return Task.FromResult(new RpcProtocolNegotiationResult(
            baseStream, tempReader, tempWriter));
    }

    readonly RpcCapabilities mMandatoryCapabilities;
    readonly RpcCapabilities mOptionalCapabilities;
    readonly Compression mCompressionFlags;
    readonly ILogger mLog;

    const byte CURRENT_VERSION = 1;

    public static readonly INegotiateRpcProtocol Instance =
        new DefaultClientProtocolNegotiation(
            mandatoryCapabilities: RpcCapabilities.None,
            optionalCapabilities: RpcCapabilities.None,
            compressionFlags: Compression.None);
}

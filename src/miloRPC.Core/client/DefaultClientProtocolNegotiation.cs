using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Net.Security;
using Microsoft.Extensions.Logging;

using miloRPC.Core.Shared;

namespace miloRPC.Core.Client;

public class DefaultClientProtocolNegotiation : INegotiateRpcProtocol
{
    public DefaultClientProtocolNegotiation(ConnectionSettings connectionSettings)
    {
        mConnectionSettings = connectionSettings;
        mLog = RpcLoggerFactory.CreateLogger("DefaultClientProtocolNegotiation");
    }

    Task<RpcProtocolNegotiationResult> INegotiateRpcProtocol.NegotiateProtocolAsync(
        uint connId,
        IPEndPoint remoteEndpoint,
        Stream baseStream)
    {
        mLog.LogDebug("Negotiating protocol for connection {ConnId}", connId);

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

    async Task<RpcProtocolNegotiationResult> NegotiateProtocolV1Async(
        uint connId,
        IPEndPoint remoteEndPoint,
        Stream baseStream,
        BinaryReader tempReader,
        BinaryWriter tempWriter)
    {
        Stream resultStream = baseStream;
        BinaryReader resultReader = tempReader;
        BinaryWriter resultWriter = tempWriter;

        RpcCapabilities mandatoryCapabilities =
            GetRpcCapabilitiesFromSettings.GetMandatory(mConnectionSettings);

        RpcCapabilities optionalCapabilities =
            GetRpcCapabilitiesFromSettings.GetOptional(mConnectionSettings);

        tempWriter.Write((byte)mandatoryCapabilities);
        tempWriter.Write((byte)optionalCapabilities);
        tempWriter.Flush();

        RpcCapabilities serverMandatory = (RpcCapabilities)tempReader.ReadByte();
        RpcCapabilities serverOptional = (RpcCapabilities)tempReader.ReadByte();

        RpcCapabilitiesNegotiationResult negotiationResult =
            RpcCapabilitiesNegotiationResult.Build(
                mandatoryCapabilities,
                optionalCapabilities,
                serverMandatory,
                serverOptional);

        if (!negotiationResult.NegotiatedOk)
        {
            string exMessage =
                $"Protocol was not correctly negotiated for conn {connId}. " +
                $"Required missing capabilities: {negotiationResult.RequiredMissingCapabilities}";
            throw new NotSupportedException(exMessage);
        }

        RemoteCertificateValidationCallback? validationCallback = null;
        if (mConnectionSettings.Ssl.CertificateValidationCallback != null)
            validationCallback = new(mConnectionSettings.Ssl.CertificateValidationCallback);

        if (negotiationResult.CommonCapabilities.HasFlag(RpcCapabilities.Ssl))
        {
            SslStream sslStream = new(resultStream, leaveInnerStreamOpen: false, validationCallback);
            await sslStream.AuthenticateAsClientAsync(remoteEndPoint.ToString());
            resultStream = sslStream;
            resultReader = new BinaryReader(resultStream);
            resultWriter = new BinaryWriter(resultStream);
        }

        if (negotiationResult.CommonCapabilities.HasFlag(RpcCapabilities.Compression))
        {
            RpcBrotliStream brotliStream = new(
                resultStream, mConnectionSettings.Compression.ArrayPool);

            resultStream = brotliStream;
            resultReader = new BinaryReader(resultStream);
            resultWriter = new BinaryWriter(resultStream);
        }

        if (mConnectionSettings.Buffering.Status is PrivateCapabilityEnablement.Enabled)
        {
            RpcBufferedStream bufferedStream = new(
                resultStream, mConnectionSettings.Buffering.BufferSize);

            resultStream = bufferedStream;
            resultReader = new BinaryReader(resultStream);
            resultWriter = new BinaryWriter(resultStream);
        }

        mLog.LogInformation(
            "Protocol was correctly negotiated for conn {ConnectionId}. " +
            "Optional missing capabilities: {OptionalMissingCapabilities}",
            connId,
            negotiationResult.OptionalMissingCapabilities);

        return new RpcProtocolNegotiationResult(resultStream, resultReader, resultWriter);
    }

    readonly ConnectionSettings mConnectionSettings;
    readonly ILogger mLog;

    const byte CURRENT_VERSION = 1;

    public static readonly INegotiateRpcProtocol Instance =
        new DefaultClientProtocolNegotiation(ConnectionSettings.None);
}

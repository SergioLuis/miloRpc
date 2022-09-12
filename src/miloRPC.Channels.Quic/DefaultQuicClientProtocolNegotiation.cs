using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using miloRPC.Core.Shared;

namespace miloRPC.Channels.Quic;

public interface INegotiateClientQuicRpcProtocol : INegotiateRpcProtocol
{
    Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? ValidateServerCertificate { get; }
    IEnumerable<SslApplicationProtocol> ApplicationProtocols { get; }
}

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("macOS")]
public class DefaultQuicClientProtocolNegotiation : INegotiateClientQuicRpcProtocol
{
    public Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? ValidateServerCertificate
        => mConnectionSettings.Ssl.CertificateValidationCallback;

    public IEnumerable<SslApplicationProtocol> ApplicationProtocols
        => mConnectionSettings.Ssl.ApplicationProtocols;

    public DefaultQuicClientProtocolNegotiation(ConnectionSettings connectionSettings)
    {
        mConnectionSettings = connectionSettings;
        mLog = RpcLoggerFactory.CreateLogger("DefaultQuicClientProtocolNegotiation");
    }

    public async Task<RpcProtocolNegotiationResult> NegotiateProtocolAsync(
        IConnectionContext ctx, Stream baseStream)
    {
        mLog.LogDebug("Negotiating protocol for connection {ConnId}", ctx.ConnectionId);

        BinaryReader tempReader = new(baseStream);
        BinaryWriter tempWriter = new(baseStream);

        tempWriter.Write((byte)CURRENT_VERSION);
        tempWriter.Flush();

        byte versionToUse = tempReader.ReadByte();

        if (versionToUse == 1)
        {
            return await NegotiateProtocolV1Async(
                ctx,
                Unsafe.As<QuicStream>(baseStream),
                tempReader,
                tempWriter);
        }

        throw new InvalidOperationException(
            $"Not prepared to negotiate protocol version {versionToUse}");
    }

    Task<RpcProtocolNegotiationResult> NegotiateProtocolV1Async(
        IConnectionContext connectionContext,
        QuicStream baseStream,
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

        RpcCapabilities serverMandatory = (RpcCapabilities) tempReader.ReadByte();
        RpcCapabilities serverOptional = (RpcCapabilities) tempReader.ReadByte();

        RpcCapabilitiesNegotiationResult negotiationResult =
            RpcCapabilitiesNegotiationResult.Build(
                mandatoryCapabilities,
                optionalCapabilities,
                serverMandatory,
                serverOptional);

        if (!negotiationResult.NegotiatedOk)
        {
            string exMessage =
                $"Protocol was not correctly negotiated for conn {connectionContext.ConnectionId}. " +
                $"Required missing capabilities: {negotiationResult.RequiredMissingCapabilities}";
            throw new NotSupportedException(exMessage);
        }

        if (negotiationResult.CommonCapabilities.HasFlag(RpcCapabilities.Compression))
        {
            RpcBrotliStream brotliStream = new(
                baseStream, mConnectionSettings.Compression.ArrayPool);

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
            connectionContext.ConnectionId,
            negotiationResult.OptionalMissingCapabilities);

        return Task.FromResult(
            new RpcProtocolNegotiationResult(resultStream, resultReader, resultWriter));
    }

    readonly ConnectionSettings mConnectionSettings;
    readonly ILogger mLog;

    const byte CURRENT_VERSION = 1;

    public static readonly INegotiateClientQuicRpcProtocol Instance =
        new DefaultQuicClientProtocolNegotiation(
            new ConnectionSettings
            {
                Ssl = new ConnectionSettings.SslSettings
                {
                    Status = SharedCapabilityEnablement.EnabledMandatory,
                    ApplicationProtocols = new []
                    {
                        new SslApplicationProtocol("miloRpc-quic")
                    }
                },
                Compression = ConnectionSettings.CompressionSettings.Disabled,
                Buffering = ConnectionSettings.BufferingSettings.Disabled
            });
}

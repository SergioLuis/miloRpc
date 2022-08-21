using System;
using System.Buffers;
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
    public Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? ValidateServerCertificate { get; }
    public IEnumerable<SslApplicationProtocol> ApplicationProtocols { get; }

    public DefaultQuicClientProtocolNegotiation(
        RpcCapabilities mandatoryCapabilities,
        RpcCapabilities optionalCapabilities) : this(
            mandatoryCapabilities,
            optionalCapabilities,
            ArrayPool<byte>.Shared,
            new List<SslApplicationProtocol> { new("miloRPC-default") }) { }

    public DefaultQuicClientProtocolNegotiation(
        RpcCapabilities mandatoryCapabilities,
        RpcCapabilities optionalCapabilities,
        ArrayPool<byte> arrayPool) : this(
            mandatoryCapabilities,
            optionalCapabilities,
            arrayPool,
            new List<SslApplicationProtocol> { new("miloRPC-default") }) { }

    public DefaultQuicClientProtocolNegotiation(
        RpcCapabilities mandatoryCapabilities,
        RpcCapabilities optionalCapabilities,
        ArrayPool<byte> arrayPool,
        IEnumerable<SslApplicationProtocol> applicationProtocols,
        Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? validateServerCertificate = null)
    {
        mMandatoryCapabilities = mandatoryCapabilities | RpcCapabilities.Ssl;
        mOptionalCapabilities = optionalCapabilities;
        mArrayPool = arrayPool;
        ValidateServerCertificate = validateServerCertificate;
        ApplicationProtocols = applicationProtocols;

        mLog = RpcLoggerFactory.CreateLogger("DefaultQuicClientProtocolNegotiation");
    }


    public async Task<RpcProtocolNegotiationResult> NegotiateProtocolAsync(
        uint connId,
        IPEndPoint remoteEndPoint,
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
            return await NegotiateProtocolV1Async(
                connId,
                remoteEndPoint,
                Unsafe.As<QuicStream>(baseStream),
                tempReader,
                tempWriter);
        }

        throw new InvalidOperationException(
            $"Not prepared to negotiate protocol version {versionToUse}");
    }

    Task<RpcProtocolNegotiationResult> NegotiateProtocolV1Async(
        uint connId,
        IPEndPoint remoteEndPoint,
        QuicStream baseStream,
        BinaryReader tempReader,
        BinaryWriter tempWriter,
        bool enableBuffering = false)
    {
        Stream resultStream = baseStream;
        BinaryReader resultReader = tempReader;
        BinaryWriter resultWriter = tempWriter;

        tempWriter.Write((byte)mMandatoryCapabilities);
        tempWriter.Write((byte)mOptionalCapabilities);
        tempWriter.Flush();

        RpcCapabilities serverMandatory = (RpcCapabilities) tempReader.ReadByte();
        RpcCapabilities serverOptional = (RpcCapabilities) tempReader.ReadByte();

        RpcCapabilitiesNegotiationResult negotiationResult =
            RpcCapabilitiesNegotiationResult.Build(
                mMandatoryCapabilities,
                mOptionalCapabilities,
                serverMandatory,
                serverOptional);

        if (!negotiationResult.NegotiatedOk)
        {
            string exMessage =
                $"Protocol was not correctly negotiated for conn {connId}. " +
                $"Required missing capabilities: {negotiationResult.RequiredMissingCapabilities}";
            throw new NotSupportedException(exMessage);
        }

        if (negotiationResult.CommonCapabilities.HasFlag(RpcCapabilities.Compression))
        {
            RpcBrotliStream brotliStream = new(baseStream, mArrayPool);
            resultStream = brotliStream;
            resultReader = new BinaryReader(resultStream);
            resultWriter = new BinaryWriter(resultStream);
        }
        
        if (enableBuffering)
        {
            RpcBufferedStream bufferedStream = new(resultStream);
            resultStream = bufferedStream;
            resultReader = new BinaryReader(resultStream);
            resultWriter = new BinaryWriter(resultStream);
        }

        mLog.LogInformation(
            "Protocol was correctly negotiated for conn {ConnectionId}. " +
            "Optional missing capabilities: {OptionalMissingCapabilities}",
            connId,
            negotiationResult.OptionalMissingCapabilities);

        return Task.FromResult(
            new RpcProtocolNegotiationResult(resultStream, resultReader, resultWriter));
    }

    readonly RpcCapabilities mMandatoryCapabilities;
    readonly RpcCapabilities mOptionalCapabilities;
    readonly ArrayPool<byte> mArrayPool;
    readonly ILogger mLog;

    const byte CURRENT_VERSION = 1;

    public static readonly INegotiateClientQuicRpcProtocol Instance =
        new DefaultQuicClientProtocolNegotiation(
            mandatoryCapabilities: RpcCapabilities.None,
            optionalCapabilities: RpcCapabilities.None);

    public static readonly Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool> AcceptAllCertificates = (_, _, _, _) => true;
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using miloRPC.Core.Shared;

namespace miloRPC.Channels.Quic;

public interface INegotiateServerQuicRpcProtocol : INegotiateRpcProtocol
{
    IEnumerable<SslApplicationProtocol> ApplicationProtocols { get; }
    X509Certificate ServerCertificate { get; }
}

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("macOS")]
public class DefaultQuicServerProtocolNegotiation : INegotiateServerQuicRpcProtocol
{
    public IEnumerable<SslApplicationProtocol> ApplicationProtocols
        => mConnectionSettings.Ssl.ApplicationProtocols;

    public X509Certificate ServerCertificate => mServerCertificate;

    public DefaultQuicServerProtocolNegotiation(ConnectionSettings connectionSettings)
    {
        mConnectionSettings = connectionSettings;
        mLog = RpcLoggerFactory.CreateLogger("DefaultQuicServerProtocolNegotiation");

        mServerCertificate = ProcessCertificateSettings(mConnectionSettings.Ssl);
    }

    public async Task<RpcProtocolNegotiationResult> NegotiateProtocolAsync(
        IConnectionContext ctx, Stream baseStream)
    {
        mLog.LogDebug("Negotiating protocol for connection {ConnId}", ctx.ConnectionId);

        BinaryReader tempReader = new(baseStream);
        BinaryWriter tempWriter = new(baseStream);

        byte clientVersion = tempReader.ReadByte();
        byte versionToUse = Math.Min(clientVersion, CURRENT_VERSION);

        tempWriter.Write((byte)versionToUse);
        tempWriter.Flush();

        if (versionToUse == 1)
        {
            return await NegotiateProtocolV1Async(
                ctx,
                baseStream,
                tempReader,
                tempWriter);
        }

        throw new InvalidOperationException(
            $"Not prepared to negotiate protocol version {versionToUse}");
    }

    Task<RpcProtocolNegotiationResult> NegotiateProtocolV1Async(
        IConnectionContext ctx,
        Stream baseStream,
        BinaryReader tempReader,
        BinaryWriter tempWriter)
    {
        Stream resultStream = baseStream;
        BinaryReader resultReader = tempReader;
        BinaryWriter resultWriter = tempWriter;

        RpcCapabilities clientMandatory = (RpcCapabilities)tempReader.ReadByte();
        RpcCapabilities clientOptional = (RpcCapabilities)tempReader.ReadByte();
        
        RpcCapabilities mandatoryCapabilities =
            GetRpcCapabilitiesFromSettings.GetMandatory(mConnectionSettings);
        RpcCapabilities optionalCapabilities =
            GetRpcCapabilitiesFromSettings.GetOptional(mConnectionSettings);

        mandatoryCapabilities |= RpcCapabilities.Ssl;

        tempWriter.Write((byte)mandatoryCapabilities);
        tempWriter.Write((byte)optionalCapabilities);
        tempWriter.Flush();

        RpcCapabilitiesNegotiationResult negotiationResult =
            RpcCapabilitiesNegotiationResult.Build(
                mandatoryCapabilities,
                optionalCapabilities,
                clientMandatory,
                clientOptional);

        if (!negotiationResult.NegotiatedOk)
        {
            throw new NotSupportedException(
                $"Protocol was not correctly negotiated for conn {ctx.ConnectionId}. "
                + $"Required missing capabilities: {negotiationResult.RequiredMissingCapabilities}.");
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
            "Protocol was correctly negotiated for conn {ConnectionId} from {RemoteEndPoint}. " +
            "Optional missing capabilities: {OptionalMissingCapabilities}",
            ctx.ConnectionId,
            ctx.RemoteEndPoint,
            negotiationResult.OptionalMissingCapabilities);

        return Task.FromResult(
            new RpcProtocolNegotiationResult(resultStream, resultReader, resultWriter));
    }

    X509Certificate ProcessCertificateSettings(ConnectionSettings.SslSettings sslSettings)
    {
        if (sslSettings.Status is not SharedCapabilityEnablement.EnabledMandatory)
            throw new ArgumentException("SslSettings.Status must be EnabledMandatory when using QUIC");

        if (string.IsNullOrEmpty(sslSettings.CertificatePassword))
            throw new ArgumentException("SSL is necessary but no cert. password is set");

        string? certificatePath = sslSettings.CertificatePath;
        if (string.IsNullOrEmpty(certificatePath))
        {
            certificatePath = Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid().ToString()[0..8] + ".pfx");

            mLog.LogWarning(
                "SSL is necessary but no cert. is specified. Going to generate a self-signed one at '{CertPath}'",
                certificatePath);
        }

        if (!File.Exists(certificatePath))
        {
            mLog.LogDebug(
                "Creating self-signed certificate on path '{CertPath}'",
                certificatePath);

            if (!TryGenerateCertificate(
                    certificatePath,
                    sslSettings.CertificatePassword,
                    out X509Certificate2? certificate))
            {
                throw new InvalidOperationException(
                    "Could not generate a self-signed certificate");
            }

            return certificate!;
        }

        if (!TryReadCertificate(
                certificatePath,
                sslSettings.CertificatePassword,
                out X509Certificate2? result))
        {
            throw new InvalidOperationException(
                "Could not load the specified certificate");
        }

        return result!;
    }

    bool TryGenerateCertificate(
        string certificatePath,
        string certificatePassword,
        out X509Certificate2? certificate)
    {
        try
        {
            string subject = "CN=" + Dns.GetHostName();
            DateTime notBefore = DateTime.UtcNow;
            DateTime notAfter = notBefore.AddYears(200);

            using RSA key = RSA.Create();
            CertificateRequest request = new(
                subject,
                key,
                HashAlgorithmName.SHA512,
                RSASignaturePadding.Pkcs1);

            certificate = request.CreateSelfSigned(notBefore, notAfter);

            File.WriteAllBytes(
                certificatePath,
                certificate.Export(
                    X509ContentType.Pfx,
                    certificatePassword));

            return true;
        }
        catch (Exception ex)
        {
            mLog.LogError(
                "There was an error generating self-signed certificate '{CertPath}': {ExMessage}",
                certificatePath, ex.Message);
            mLog.LogDebug("StackTrace: {ExStackTrace}", ex.StackTrace);

            certificate = null;
            return false;
        }
    }

    bool TryReadCertificate(
        string certificatePath,
        string certificatePassword,
        out X509Certificate2? result)
    {
        try
        {
            result = new X509Certificate2(
                certificatePath,
                certificatePassword,
                X509KeyStorageFlags.Exportable);
            return true;
        }
        catch (Exception ex)
        {
            mLog.LogError(
                "Could not read certificate '{CertPath}': {ExMessage}",
                certificatePath, ex.Message);
        }

        result = null;
        return false;
    }

    readonly ConnectionSettings mConnectionSettings;
    readonly X509Certificate mServerCertificate;
    readonly ILogger mLog;

    const byte CURRENT_VERSION = 1;

    public static readonly INegotiateServerQuicRpcProtocol Instance =
        new DefaultQuicServerProtocolNegotiation(
            new ConnectionSettings
            {
                Ssl = new ConnectionSettings.SslSettings
                {
                    Status = SharedCapabilityEnablement.EnabledMandatory,
                    CertificatePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                        "miloRpc-autogen.pfx"),
                    CertificatePassword = "079b5ef9-dc5e-4a48-a2e3-403d7456c495",
                    ApplicationProtocols = new []
                    {
                        new SslApplicationProtocol("miloRpc-quic")
                    }
                },
                Compression = ConnectionSettings.CompressionSettings.Disabled,
                Buffering = ConnectionSettings.BufferingSettings.Disabled
            });
}

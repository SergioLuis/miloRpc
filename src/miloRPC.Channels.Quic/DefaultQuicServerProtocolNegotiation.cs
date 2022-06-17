using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
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
    X509Certificate GetServerCertificate();
}

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("macOS")]
public class DefaultQuicServerProtocolNegotiation : INegotiateServerQuicRpcProtocol
{
    public IEnumerable<SslApplicationProtocol> ApplicationProtocols { get; }

    public DefaultQuicServerProtocolNegotiation(
        RpcCapabilities mandatoryCapabilities,
        RpcCapabilities optionalCapabilities,
        string certificatePath,
        string certificatePassword) : this(
            mandatoryCapabilities,
            optionalCapabilities,
            ArrayPool<byte>.Shared,
            new List<SslApplicationProtocol> { new("miloRPC-default") },
            certificatePath,
            certificatePassword) { }

    public DefaultQuicServerProtocolNegotiation(
        RpcCapabilities mandatoryCapabilities,
        RpcCapabilities optionalCapabilities,
        ArrayPool<byte> arrayPool,
        IEnumerable<SslApplicationProtocol> applicationProtocols,
        string certificatePath,
        string certificatePassword)
    {
        mMandatoryCapabilities = mandatoryCapabilities | RpcCapabilities.Ssl;
        mOptionalCapabilities = optionalCapabilities;
        mArrayPool = arrayPool;
        ApplicationProtocols = applicationProtocols;
        mLog = RpcLoggerFactory.CreateLogger("DefaultQuicServerProtocolNegotiation");

        mServerCertificate = ProcessCertificateSettings(certificatePath, certificatePassword);
    }

    public X509Certificate GetServerCertificate()
    {
        Contract.Assert(mServerCertificate is not null);
        return mServerCertificate;
    }

    public async Task<RpcProtocolNegotiationResult> NegotiateProtocolAsync(
        uint connId, IPEndPoint remoteEndPoint, Stream baseStream)
    {
        mLog.LogDebug("Negotiating protocol for connection {ConnId}", connId);

        BinaryReader tempReader = new(baseStream);
        BinaryWriter tempWriter = new(baseStream);

        byte clientVersion = tempReader.ReadByte();
        byte versionToUse = Math.Min(clientVersion, CURRENT_VERSION);

        tempWriter.Write((byte)versionToUse);
        tempWriter.Flush();

        if (versionToUse == 1)
        {
            return await NegotiateProtocolV1Async(
                connId,
                remoteEndPoint,
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
        Stream resultStream = baseStream;
        BinaryReader resultReader = tempReader;
        BinaryWriter resultWriter = tempWriter;

        RpcCapabilities clientMandatory = (RpcCapabilities)tempReader.ReadByte();
        RpcCapabilities clientOptional = (RpcCapabilities)tempReader.ReadByte();

        tempWriter.Write((byte)mMandatoryCapabilities);
        tempWriter.Write((byte)mOptionalCapabilities);
        tempWriter.Flush();

        RpcCapabilitiesNegotiationResult negotiationResult =
            RpcCapabilitiesNegotiationResult.Build(
                mMandatoryCapabilities,
                mOptionalCapabilities,
                clientMandatory,
                clientOptional);

        if (!negotiationResult.NegotiatedOk)
        {
            throw new NotSupportedException(
                $"Protocol was not correctly negotiated for conn {connId}. "
                + $"Required missing capabilities: {negotiationResult.RequiredMissingCapabilities}.");
        }
        if (negotiationResult.CommonCapabilities.HasFlag(RpcCapabilities.Compression))
        {
            RpcBrotliStream brotliStream = new(resultStream, mArrayPool);
            resultStream = brotliStream;
            resultReader = new BinaryReader(resultStream);
            resultWriter = new BinaryWriter(resultStream);
        }

        mLog.LogInformation(
            "Protocol was correctly negotiated for conn {ConnectionId} from {RemoteEndPoint}. " +
            "Optional missing capabilities: {OptionalMissingCapabilities}",
            connId, remoteEndPoint, negotiationResult.OptionalMissingCapabilities);

        return Task.FromResult(
            new RpcProtocolNegotiationResult(resultStream, resultReader, resultWriter));
    }

    X509Certificate? ProcessCertificateSettings(string certificatePath, string certificatePassword)
    {
        if (string.IsNullOrEmpty(certificatePassword))
            throw new ArgumentException("SSL is necessary but no cert. password is set");

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
                    certificatePassword,
                    out X509Certificate2? certificate))
            {
                throw new InvalidOperationException(
                    "Could not generate a self-signed certificate");
            }

            return certificate;
        }

        if (!TryReadCertificate(
                certificatePath,
                certificatePassword,
                out X509Certificate2? result))
        {
            throw new InvalidOperationException(
                "Could not load the specified certificate");
        }

        return result;
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
        }

        certificate = null;
        return false;
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

    readonly RpcCapabilities mMandatoryCapabilities;
    readonly RpcCapabilities mOptionalCapabilities;
    readonly ArrayPool<byte> mArrayPool;
    readonly X509Certificate? mServerCertificate;
    readonly ILogger mLog;

    const byte CURRENT_VERSION = 1;

    public static readonly INegotiateServerQuicRpcProtocol Instance =
        new DefaultQuicServerProtocolNegotiation(
            mandatoryCapabilities: RpcCapabilities.None,
            optionalCapabilities: RpcCapabilities.None,
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "miloRpc-autogen.pfx"),
            "079b5ef9-dc5e-4a48-a2e3-403d7456c495");
}

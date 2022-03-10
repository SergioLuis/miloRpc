using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Server;

public class DefaultServerProtocolNegotiation : INegotiateRpcProtocol
{
    public DefaultServerProtocolNegotiation(
        RpcCapabilities mandatoryCapabilities,
        RpcCapabilities optionalCapabilities) : this(
            mandatoryCapabilities,
            optionalCapabilities,
            ArrayPool<byte>.Shared,
            string.Empty,
            string.Empty)
    { }

    public DefaultServerProtocolNegotiation(
        RpcCapabilities mandatoryCapabilities,
        RpcCapabilities optionalCapabilites,
        ArrayPool<byte> arrayPool) : this(
            mandatoryCapabilities,
            optionalCapabilites,
            arrayPool,
            string.Empty,
            string.Empty) { }

    public DefaultServerProtocolNegotiation(
        RpcCapabilities mandatoryCapabilities,
        RpcCapabilities optionalCapabilities,
        ArrayPool<byte> arrayPool,
        string certificatePath,
        string certificatePassword)
    {
        mMandatoryCapabilities = mandatoryCapabilities;
        mOptionalCapabilities = optionalCapabilities;
        mArrayPool = arrayPool;
        mLog = RpcLoggerFactory.CreateLogger("DefaultServerProtocolNegotiation");

        mServerCertificate = ProcessCertificateSettings(
            mMandatoryCapabilities,
            mOptionalCapabilities,
            certificatePath,
            certificatePassword);
    }

    Task<RpcProtocolNegotiationResult> INegotiateRpcProtocol.NegotiateProtocolAsync(
        uint connId,
        IPEndPoint remoteEndPoint,
        Stream baseStream)
    {
        BinaryReader tempReader = new(baseStream);
        BinaryWriter tempWriter = new(baseStream);

        byte clientVersion = tempReader.ReadByte();
        byte versionToUse = Math.Min(clientVersion, CURRENT_VERSION);

        tempWriter.Write((byte)versionToUse);
        tempWriter.Flush();

        if (versionToUse == 1)
        {
            return NegotiateProtocolV1Async(
                connId,
                remoteEndPoint,
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

        if (negotiationResult.CommonCapabilities.HasFlag(RpcCapabilities.Ssl))
        {
            SslStream sslStream = new(resultStream, leaveInnerStreamOpen: false);
            await sslStream.AuthenticateAsServerAsync(mServerCertificate!);
            resultStream = sslStream;
        }

        if (negotiationResult.CommonCapabilities.HasFlag(RpcCapabilities.Compression))
        {
            RpcBrotliStream brotliStream = new(resultStream, mArrayPool);
            resultStream = brotliStream;
        }

        mLog.LogInformation(
            "Protocol was correctly negotiated for conn {0}. " +
            "Optional missing capabilities: {1}",
            connId,
            negotiationResult.OptionalMissingCapabilities);

        return new RpcProtocolNegotiationResult(
            resultStream, tempReader, tempWriter);
    }

    X509Certificate? ProcessCertificateSettings(
        RpcCapabilities mandatory,
        RpcCapabilities optional,
        string certificatePath,
        string certificatePassword)
    {
        bool isSslNecessary =
            ((mandatory | optional) & RpcCapabilities.Ssl) == RpcCapabilities.Ssl;

        if (!isSslNecessary)
            return null;

        if (string.IsNullOrEmpty(certificatePassword))
            throw new ArgumentException("SSL is necessary but no cert. password is set");

        if (string.IsNullOrEmpty(certificatePath))
        {
            certificatePath = Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid().ToString()[0..8] + ".pfx");

            mLog.LogWarning(
                "SSL is necessary but no cert. is specified. Going to generate a self-signed one at '{0}'",
                certificatePath);
        }

        if (!File.Exists(certificatePath))
        {
            mLog.LogDebug(
                "Creating self-signed certificate on path '{0}'",
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
                "There was an error generating self-signed certificate '{0}': {1}",
                certificatePath, ex.Message);
            mLog.LogDebug(
                "StackTrace:{0}{1}",
                Environment.NewLine, ex.StackTrace);
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
            result = new X509Certificate2(certificatePath, certificatePassword);
            return true;
        }
        catch (Exception ex)
        {
            mLog.LogError(
                "Could not read certificate '{0}': {1}",
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

    public static readonly INegotiateRpcProtocol Instance =
        new DefaultServerProtocolNegotiation(
            mandatoryCapabilities: RpcCapabilities.None,
            optionalCapabilities: RpcCapabilities.None);
}

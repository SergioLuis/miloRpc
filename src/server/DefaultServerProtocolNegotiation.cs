using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Shared;
using RpcCapabilities = dotnetRpc.Shared.DefaultProtocolNegotiation.RpcCapabilities;
using Compression = dotnetRpc.Shared.DefaultProtocolNegotiation.Compression;

namespace dotnetRpc.Server;

public class DefaultServerProtocolNegotiation : INegotiateRpcProtocol
{
    byte INegotiateRpcProtocol.CurrentProtocolVersion => CURRENT_VERSION;

    bool INegotiateRpcProtocol.CanHandleProtocolVersion(int version) => version == CURRENT_VERSION;

    public DefaultServerProtocolNegotiation(
        RpcCapabilities mandatoryCapabilities,
        RpcCapabilities optionalCapabilities,
        Compression compressionFlags) : this(
            mandatoryCapabilities,
            optionalCapabilities,
            compressionFlags,
            string.Empty,
            string.Empty) { }

    public DefaultServerProtocolNegotiation(
        RpcCapabilities mandatoryCapabilities,
        RpcCapabilities optionalCapabilities,
        Compression compressionFlags,
        string certificatePath,
        string certificatePassword)
    {
        mLog = RpcLoggerFactory.CreateLogger("DefaultServerProtocolNegotiation");
        mMandatoryCapabilities = mandatoryCapabilities;
        mOptionalCapabilities = optionalCapabilities;
        mCompressionFlags = compressionFlags;
        mServerCertificate = ProcessCertificateSettings(
            mMandatoryCapabilities,
            mOptionalCapabilities,
            certificatePath,
            certificatePassword);
    }

    Task<RpcProtocolNegotiationResult> INegotiateRpcProtocol.NegotiateProtocolAsync(
        int version,
        IPEndPoint remoteEndPoint,
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
            certificatePath = Path.GetTempFileName();
            mLog.LogWarning(
                "SSL is necessary but no cert. is specified. Going to generate a self-signed one at '{0}'",
                certificatePath);
            // TODO: Generate the certificate
        }

        // TODO: Load the certificate
        return null;
    }

    readonly ILogger mLog;
    readonly RpcCapabilities mMandatoryCapabilities;
    readonly RpcCapabilities mOptionalCapabilities;
    readonly Compression mCompressionFlags;
    readonly X509Certificate? mServerCertificate;
    const byte CURRENT_VERSION = 1;
}

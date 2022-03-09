using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Server;

public class DefaultServerProtocolNegotiation : INegotiateRpcProtocol
{
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
        mMandatoryCapabilities = mandatoryCapabilities;
        mOptionalCapabilities = optionalCapabilities;
        mCompressionFlags = compressionFlags;
        mServerCertificate = ProcessCertificateSettings(
            mMandatoryCapabilities,
            mOptionalCapabilities,
            certificatePath,
            certificatePassword);
        
        mLog = RpcLoggerFactory.CreateLogger("DefaultServerProtocolNegotiation");
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

    Task<RpcProtocolNegotiationResult> NegotiateProtocolV1Async(
        uint connId,
        IPEndPoint remoteEndPoint,
        Stream baseStream,
        BinaryReader tempReader,
        BinaryWriter tempWriter)
    {
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

    readonly RpcCapabilities mMandatoryCapabilities;
    readonly RpcCapabilities mOptionalCapabilities;
    readonly Compression mCompressionFlags;
    readonly X509Certificate? mServerCertificate;
    readonly ILogger mLog;

    const byte CURRENT_VERSION = 1;

    public static readonly INegotiateRpcProtocol Instance =
        new DefaultServerProtocolNegotiation(
            mandatoryCapabilities: RpcCapabilities.None,
            optionalCapabilities: RpcCapabilities.None,
            compressionFlags: Compression.None);
}

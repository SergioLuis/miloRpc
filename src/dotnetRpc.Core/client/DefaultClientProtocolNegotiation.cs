using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Client;

public class DefaultClientProtocolNegotiation : INegotiateRpcProtocol
{
    public DefaultClientProtocolNegotiation(
        RpcCapabilities mandatoryCapabilities,
        RpcCapabilities optionalCapabilities,
        Compression compressionFlags) : this(
            mandatoryCapabilities,
            optionalCapabilities,
            compressionFlags, null) { }

    public DefaultClientProtocolNegotiation(
        RpcCapabilities mandatoryCapabilities,
        RpcCapabilities optionalCapabilities,
        Compression compressionFlags,
        Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? validateServerCertificate)
    {
        mMandatoryCapabilities = mandatoryCapabilities;
        mOptionalCapabilities = optionalCapabilities;
        mCompressionFlags = compressionFlags;
        mValidateServerCertificate = validateServerCertificate;

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

    async Task<RpcProtocolNegotiationResult> NegotiateProtocolV1Async(
        uint connId,
        IPEndPoint remoteEndPoint,
        Stream baseStream,
        BinaryReader tempReader,
        BinaryWriter tempWriter)
    {
        Stream resultStream = baseStream;

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

        RemoteCertificateValidationCallback? validationCallback = null;
        if (mValidateServerCertificate != null)
            validationCallback = new(mValidateServerCertificate);

        if (negotiationResult.CommonCapabilities.HasFlag(RpcCapabilities.Compression))
        {
            SslStream sslStream = new(resultStream, leaveInnerStreamOpen: false, validationCallback);
            await sslStream.AuthenticateAsClientAsync(remoteEndPoint.ToString());
            resultStream = sslStream;
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

        return new RpcProtocolNegotiationResult(
            resultStream, tempReader, tempWriter);
    }

    readonly RpcCapabilities mMandatoryCapabilities;
    readonly RpcCapabilities mOptionalCapabilities;
    readonly Compression mCompressionFlags;
    readonly Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? mValidateServerCertificate;
    readonly ILogger mLog;

    const byte CURRENT_VERSION = 1;

    public static readonly INegotiateRpcProtocol Instance =
        new DefaultClientProtocolNegotiation(
            mandatoryCapabilities: RpcCapabilities.None,
            optionalCapabilities: RpcCapabilities.None,
            compressionFlags: Compression.None);
}

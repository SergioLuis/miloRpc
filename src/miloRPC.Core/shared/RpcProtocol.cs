using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace miloRPC.Core.Shared;

public enum MethodCallResult : byte
{
    Ok           = 0,
    Failed       = 1,
    NotSupported = 2
}

public class RpcProtocolNegotiationResult
{
    internal Stream Stream { get; }
    internal BinaryReader Reader { get; }
    internal BinaryWriter Writer { get; }

    public RpcProtocolNegotiationResult(
        Stream stream, BinaryReader reader, BinaryWriter writer)
    {
        Stream = stream;
        Reader = reader;
        Writer = writer;
    }
}

public interface IMethodId
{
    string Name { get; }

    void SetSolvedMethodName(string? name);
}

public enum SharedCapabilityEnablement : byte
{
    Disabled,
    EnabledOptional,
    EnabledMandatory
}

public enum PrivateCapabilityEnablement : byte
{
    Disabled,
    Enabled
}

public class ConnectionSettings
{
    public SslSettings Ssl { get; set; } = SslSettings.Disabled;
    public CompressionSettings Compression { get; set; } = CompressionSettings.Disabled;
    public BufferingSettings Buffering { get; set; } = BufferingSettings.Disabled;

    public static readonly ConnectionSettings None = new();

    public class SslSettings
    {
        public SharedCapabilityEnablement Status { get; init; } = SharedCapabilityEnablement.Disabled;
        public string? CertificatePath { get; init; } = string.Empty;
        public string? CertificatePassword { get; init; } = string.Empty;
        public IEnumerable<SslApplicationProtocol> ApplicationProtocols { get; init; } = Array.Empty<SslApplicationProtocol>();
        public Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? CertificateValidationCallback { get; init; } = null;

        public static readonly SslSettings Disabled = new();

        public static readonly Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool> AcceptAllCertificates = (_, _, _, _) => true;
    }

    public class CompressionSettings
    {
        public SharedCapabilityEnablement Status { get; init; } = SharedCapabilityEnablement.Disabled;
        public ArrayPool<byte> ArrayPool { get; init; } = ArrayPool<byte>.Shared;
        
        public static readonly CompressionSettings Disabled = new();
    }

    public class BufferingSettings
    {
        public PrivateCapabilityEnablement Status { get; init; } = PrivateCapabilityEnablement.Disabled;
        public int BufferSize { get; init; } = -1;

        public static readonly BufferingSettings Disabled = new();

        public static readonly BufferingSettings EnabledRecommended = new()
        {
            Status = PrivateCapabilityEnablement.Enabled,
            BufferSize = 4096
        };
    }
}

public interface INegotiateRpcProtocol
{
    Task<RpcProtocolNegotiationResult> NegotiateProtocolAsync(
        uint connId,
        IPEndPoint remoteEndPoint,
        Stream baseStream);
}

using System;
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

public class ConnectionSettings
{
    public SslSettings Ssl { get; init; } = SslSettings.None;
    public BufferingSettings Buffering { get; init; } = BufferingSettings.None;

    public static readonly ConnectionSettings None = new();

    public class SslSettings
    {
        public string? CertificatePath { get; init; } = string.Empty;
        public string? CertificatePassword { get; init; } = string.Empty;
        public Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? CertificateValidationCallback { get; init; } = null;

        public static readonly SslSettings None = new();

        public static readonly Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool> AcceptAllCertificates = (_, _, _, _) => true;
    }

    public class BufferingSettings
    {
        public bool Enable { get; init; } = false;
        public int BufferSize { get; init; } = -1;

        public static readonly BufferingSettings None = new();

        public static readonly BufferingSettings Recommended = new()
        {
            Enable = true,
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

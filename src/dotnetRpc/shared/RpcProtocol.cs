using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace dotnetRpc.Shared;

public class RpcProtocolNegotiationResult
{
    internal Stream Stream { get; private set; }
    internal BinaryReader Reader { get; private set; }
    internal BinaryWriter Writer { get; private set; }

    public RpcProtocolNegotiationResult(
        Stream stream, BinaryReader reader, BinaryWriter writer)
    {
        Stream = stream;
        Reader = reader;
        Writer = writer;
    }
}

public interface INetworkMessage
{
    void Serialize(BinaryWriter writer);
    void Deserialize(BinaryReader reader);
}

public record RpcNetworkMessages(INetworkMessage Request, INetworkMessage Response);

public interface IMethodId
{
    string Name { get; }

    void SetSolvedMethodName(string? name);
}

public interface INegotiateRpcProtocol
{
    Task<RpcProtocolNegotiationResult> NegotiateProtocolAsync(
        uint connId,
        IPEndPoint remoteEndPoint,
        Stream baseStream);
}

public interface IReadMethodId
{
    IMethodId ReadMethodId(BinaryReader reader);
}

public interface IWriteMethodId
{
    void WriteMethodId(BinaryWriter writer, IMethodId methodId);
}

public interface IWriteMethodCallResult
{
    void WriteOkMethodCallResult(BinaryWriter writer);
    void WriteFailedMethodCallResult(BinaryWriter writer, Exception ex);
    void WriteNotSupportedMethodCallResult(BinaryWriter writer);
}

public interface IReadMethodCallResult
{
    void ReadMethodCallResult(
        BinaryReader reader,
        out bool isResultAvailable,
        out Exception? ex);
}

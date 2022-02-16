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

public class BaseMethodId { }

public interface INegotiateRpcProtocol
{
    Task<RpcProtocolNegotiationResult> NegotiateProtocolAsync(
        uint connId,
        IPEndPoint remoteEndPoint,
        Stream baseStream);
}

public interface IReadMethodId
{
    BaseMethodId ReadMethodId(BinaryReader reader);
}

public interface IWriteMethodCallResult
{
    void WriteOkMethodCallResult(BinaryWriter writer);
    void WriteFailedMethodCallResult(BinaryWriter writer, Exception ex);
    void WriteNotSupportedMethodCallResult(BinaryWriter writer);
}

public interface IWriteMethodId
{
    void WriteMethodId(BinaryWriter writer, BaseMethodId methodId);
}

public interface IReadMethodCallResult
{
    void ReadMethodCallResult(
        BinaryReader reader,
        out bool isResultAvailable,
        out Exception? ex);
}

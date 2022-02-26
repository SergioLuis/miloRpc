using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace dotnetRpc.Core.Shared;

public enum MethodCallResult : byte
{
    OK           = 0,
    Failed       = 1,
    NotSupported = 2
}


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



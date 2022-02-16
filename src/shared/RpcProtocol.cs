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

public interface INegotiateRpcProtocol
{
    Task<RpcProtocolNegotiationResult> NegotiateProtocolAsync(
        uint connId,
        IPEndPoint remoteEndPoint,
        Stream baseStream);
}

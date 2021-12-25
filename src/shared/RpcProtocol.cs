using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace dotnetRpc.Shared;

public class RpcProtocolNegotiationResult
{
    public Stream Stream { get; private set; }
    public BinaryReader Reader { get; private set; }
    public BinaryWriter Writer { get; private set; }

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
    byte CurrentProtocolVersion { get; }
    bool CanHandleProtocolVersion(int version);
    Task<RpcProtocolNegotiationResult> NegotiateProtocolAsync(
        int version,
        IPEndPoint remoteEndPoint,
        Stream baseStream,
        BinaryReader tempReader,
        BinaryWriter tempWriter);
}

public class RpcProtocol
{
    public RpcProtocol(
        Stream baseStream,
        IPEndPoint remoteEndPoint,
        INegotiateRpcProtocol negotiateConnection)
    {
        mBaseStream = baseStream;
        mReader = new(baseStream);
        mWriter = new(baseStream);
        mRemoteEndPoint = remoteEndPoint;
        mNegotiateConnection = negotiateConnection;
    }

    public async Task NegotiateConnectionFromClientAsync()
    {
        byte clientVersion = mReader.ReadByte();
        byte versionToUse = Math.Min(
            (byte)mNegotiateConnection.CurrentProtocolVersion,
            (byte)clientVersion);
        mWriter.Write((byte)versionToUse);
        mWriter.Flush();

        if (!mNegotiateConnection.CanHandleProtocolVersion(versionToUse))
        {
            throw new NotSupportedException(
                $"Protocol version {versionToUse} is not supported");
        }

        var result = await mNegotiateConnection.NegotiateProtocolAsync(
            versionToUse,
            mRemoteEndPoint,
            mBaseStream,
            mReader,
            mWriter);

        mBaseStream = result.Stream;
        mReader = result.Reader;
        mWriter = result.Writer;
    }

    public async Task NegotiateConnectionToServerAsync()
    {
        BinaryReader tempReader = new(mBaseStream);
        BinaryWriter tempWriter = new(mBaseStream);

        tempWriter.Write((byte)mNegotiateConnection.CurrentProtocolVersion);
        tempWriter.Flush();
        byte versionToUse = tempReader.ReadByte();

        if (!mNegotiateConnection.CanHandleProtocolVersion(versionToUse))
        {
            throw new NotSupportedException(
                $"Protocol version {versionToUse} is not supported");
        }

        var result = await mNegotiateConnection.NegotiateProtocolAsync(
            versionToUse,
            mRemoteEndPoint,
            mBaseStream,
            mReader,
            mWriter);

        mBaseStream = result.Stream;
        mReader = result.Reader;
        mWriter = result.Writer;
    }

    Stream mBaseStream;
    BinaryReader mReader;
    BinaryWriter mWriter;

    readonly IPEndPoint mRemoteEndPoint;
    readonly INegotiateRpcProtocol mNegotiateConnection;
}

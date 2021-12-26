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
    byte CurrentProtocolVersion { get; }
    bool CanHandleProtocolVersion(int version);
    Task<RpcProtocolNegotiationResult> NegotiateProtocolAsync(
        uint connId,
        IPEndPoint remoteEndPoint,
        int version,
        Stream baseStream,
        BinaryReader tempReader,
        BinaryWriter tempWriter);
}

public class RpcProtocol
{
    public RpcProtocol(
        uint connId,
        IPEndPoint remoteEndPoint,
        Stream baseStream,
        INegotiateRpcProtocol negotiateConnection)
    {
        mConnId = connId;
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
            mConnId,
            mRemoteEndPoint,
            versionToUse,
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
            mConnId,
            mRemoteEndPoint,
            versionToUse,
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

    readonly uint mConnId;
    readonly IPEndPoint mRemoteEndPoint;
    readonly INegotiateRpcProtocol mNegotiateConnection;
}

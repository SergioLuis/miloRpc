using System;
using System.IO;
using System.Net;

namespace dotnetRpc.Shared;

public interface INegotiateProtocol
{
    byte CurrentProtocolVersion { get; }
    bool CanHandleProtocolVersion(int version);
    void NegotiateProtocol(
        int version,
        IPEndPoint remoteEndPoint,
        Stream baseStream,
        BinaryReader tempReader,
        BinaryWriter tempWriter,
        out Stream resultStream,
        out BinaryReader newReader,
        out BinaryWriter newWriter);
}

public class RpcProtocol
{
    public RpcProtocol(
        Stream baseStream,
        IPEndPoint remoteEndPoint,
        INegotiateProtocol negotiateConnection)
    {
        mBaseStream = baseStream;
        mReader = new(baseStream);
        mWriter = new(baseStream);
        mRemoteEndPoint = remoteEndPoint;
        mNegotiateConnection = negotiateConnection;
    }

    public void BeginConnectionFromClient()
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

        mNegotiateConnection.NegotiateProtocol(
            versionToUse,
            mRemoteEndPoint,
            mBaseStream,
            mReader,
            mWriter,
            out mBaseStream,
            out mReader,
            out mWriter);
    }

    public void BeginConnectionToServer()
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

        mNegotiateConnection.NegotiateProtocol(
            versionToUse,
            mRemoteEndPoint,
            mBaseStream,
            mReader,
            mWriter,
            out mBaseStream,
            out mReader,
            out mWriter);
    }

    Stream mBaseStream;
    BinaryReader mReader;
    BinaryWriter mWriter;

    readonly IPEndPoint mRemoteEndPoint;
    readonly INegotiateProtocol mNegotiateConnection;
}

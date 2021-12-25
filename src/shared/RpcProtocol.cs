using System;
using System.IO;
using System.Net;

namespace dotnetRpc.Shared;

public class RpcProtocol : IDisposable
{
    public BinaryReader Reader => mReader;
    public BinaryWriter Writer => mWriter;

    public RpcProtocol(
        IPEndPoint remoteEndPoint,
        BinaryReader reader,
        BinaryWriter writer)
    {
        mRemoteEndPoint = remoteEndPoint;
        mReader = reader;
        mWriter = writer;
    }

    public bool BeginConnectionFromClient()
    {
        byte clientVersion = mReader.ReadByte();
        byte versionToUse = Math.Min(
            (byte)CURRENT_VERSION, (byte)clientVersion);
        mWriter.Write((byte)versionToUse);
        mWriter.Flush();

        // TODO: Negotiate protocol

        throw new NotSupportedException(
            "Something catastrophic happened negotiating protocol version. "
            + "If you are reading this exception, pray whatever you know.");
    }

    public bool BeginConnectionToServer()
    {
        mWriter.Write((byte)CURRENT_VERSION);
        mWriter.Flush();
        byte versionToUse = mReader.ReadByte();

        // TODO: Negotiate protocol

        throw new NotSupportedException(
            "Something catastrophic happened negotiating protocol version. "
            + "If you are reading this exception, pray whatever you know.");
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (mbDisposed)
            return;

        if (disposing)
        {
            mReader.Dispose();
            mWriter.Dispose();
        }
        mbDisposed = true;
    }

    bool mbDisposed = false;

    readonly IPEndPoint mRemoteEndPoint;
    readonly BinaryReader mReader;
    readonly BinaryWriter mWriter;

    const byte CURRENT_VERSION = 1;
}

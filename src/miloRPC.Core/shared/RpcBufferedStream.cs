using System.IO;

namespace miloRPC.Core.Shared;

/*
 * From BufferedStream documentation:
 *
 *   > https://github.com/dotnet/runtime/blob/e55c908229e36f99a52745d4ee85316a0e8bb6a2/src/libraries/System.Private.CoreLib/src/System/IO/BufferedStream.cs
 *
 *   > The assumption here is you will almost always be doing a series of reads
 *   > or writes, but rarely alternate between the two of them on the same stream.
 *
 * This is not the case in miloRpc.
 * We are constantly alternating between reads and writes over the same (network)
 * stream - which would trash BufferedStream's buffer usage.
 */
public class RpcBufferedStream : Stream
{
    public override bool CanRead => mInputStream.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => mOutputStream.CanWrite;

    public override long Length => throw new System.NotImplementedException();

    public override long Position
    {
        get => throw new System.NotImplementedException();
        set => throw new System.NotImplementedException();
    }

    public RpcBufferedStream(Stream stream, int bufferSize = 4096)
    {
        mUnderlyingStream = stream;
        mOutputStream = new BufferedStream(stream, bufferSize);
        mInputStream = new BufferedStream(stream, bufferSize);
    }

    public override void Flush()
    {
        mOutputStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        #warning Reads with 0 count might return immediately when done against the BufferedStream.
        return mInputStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new System.NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new System.NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        mOutputStream.Write(buffer, offset, count);
    }

    readonly Stream mUnderlyingStream;
    readonly BufferedStream mOutputStream;
    readonly BufferedStream mInputStream;
}

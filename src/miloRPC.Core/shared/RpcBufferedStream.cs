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
    public override bool CanRead => throw new System.NotImplementedException();

    public override bool CanSeek => throw new System.NotImplementedException();

    public override bool CanWrite => throw new System.NotImplementedException();

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
        throw new System.NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new System.NotImplementedException();
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
        throw new System.NotImplementedException();
    }

    readonly Stream mUnderlyingStream;
    readonly BufferedStream mOutputStream;
    readonly BufferedStream mInputStream;
}

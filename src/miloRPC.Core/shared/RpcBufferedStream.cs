using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
 * stream - which would (seemingly) trash BufferedStream's buffer usage.
 */
public class RpcBufferedStream : Stream
{
    public override bool CanRead => mInputStream.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => mOutputStream.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public RpcBufferedStream(Stream stream, int bufferSize = 4096)
    {
        mOutputStream = new BufferedStream(stream, bufferSize);
        mInputStream = new BufferedStream(stream, bufferSize);
    }

    public override int Read(byte[] buffer, int offset, int count)
        =>  mInputStream.Read(buffer, offset, count);

    public override Task<int> ReadAsync(
        byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => mInputStream.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken cancellationToken = new())
        => mInputStream.ReadAsync(buffer, cancellationToken);

    public override void Write(byte[] buffer, int offset, int count)
        => mOutputStream.Write(buffer, offset, count);

    public override Task WriteAsync(
        byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => mOutputStream.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = new())
        => mOutputStream.WriteAsync(buffer, cancellationToken);

    public override void Flush()
        => mOutputStream.Flush();

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    readonly BufferedStream mOutputStream;
    readonly BufferedStream mInputStream;
}

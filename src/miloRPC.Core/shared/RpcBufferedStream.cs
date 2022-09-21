using System;
using System.Buffers;
using System.Diagnostics.Contracts;
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

class NewRpcBufferedStream : Stream
{
    public override bool CanRead => mInnerStream.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => mInnerStream.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    
    public NewRpcBufferedStream(Stream stream)
        : this(stream, ArrayPool<byte>.Shared) { }

    public NewRpcBufferedStream(
        Stream stream,
        ArrayPool<byte> arrayPool,
        bool leaveStreamOpen = false,
        int bufferSize = 4096)
    {
        mInnerStream = stream;
        mArrayPool = arrayPool;
        mLeaveInnerStreamOpen = leaveStreamOpen;
        mBufferSize = bufferSize;
        
        mWriteBuffer = arrayPool.Rent(bufferSize);
        mReadBuffer = arrayPool.Rent(bufferSize);
    }

    public void EnableBuffering()
    {
        if (mBufferingEnabled)
            return;

        Contract.Assert(mWriteBuffer is null);
        Contract.Assert(mReadBuffer is null);

        mWriteBuffer = mArrayPool.Rent(mBufferSize);
        mReadBuffer = mArrayPool.Rent(mBufferSize);

        mBufferingEnabled = true;
    }

    public void DisableBuffering()
    {
        if (!mBufferingEnabled)
            return;

        Contract.Assert(mWriteBuffer is not null);
        Contract.Assert(mWriteBufferPos == 0);

        Contract.Assert(mReadBuffer is not null);
        Contract.Assert(mReadBufferLen == mReadBufferPos);
        
        mArrayPool.Return(mWriteBuffer);
        mArrayPool.Return(mReadBuffer);

        mWriteBuffer = null;
        mReadBuffer = null;
        mBufferingEnabled = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (mReadBuffer is not null)
                mArrayPool.Return(mReadBuffer);
            
            if (mWriteBuffer is not null)
                mArrayPool.Return(mWriteBuffer);
            
            if (!mLeaveInnerStreamOpen)
                mInnerStream.Dispose();
        }
        base.Dispose(disposing);
    }

    public override void Flush()
    {
        if (mWriteBufferPos == 0) // Nothing left to write
            return;

        mInnerStream.Write(mWriteBuffer.AsSpan(0, mWriteBufferPos));
        mInnerStream.Flush();
        mWriteBufferPos = 0;
    }

    public override async Task FlushAsync(CancellationToken ct)
    {
        if (mWriteBufferPos == 0) // Nothing left to write
            return;

        await mInnerStream.WriteAsync(mWriteBuffer.AsMemory(0, mWriteBufferPos), ct);
        await mInnerStream.FlushAsync(ct);
        mWriteBufferPos = 0;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        Contract.Assert(mReadBuffer is not null == mBufferingEnabled);

        Contract.Assert(buffer is not null);
        Contract.Assert(offset >= 0 && offset < buffer.Length);
        Contract.Assert(count >= 0);
        Contract.Assert(buffer.Length - offset < count);

        if (mReadBuffer is null)
            return mInnerStream.Read(buffer, offset, count);

        int availableToRead = mReadBufferLen - mReadBufferPos;

        // No content available in the read buffer
        if (availableToRead == 0)
        {
            mReadBufferPos = 0;
            mReadBufferLen = mInnerStream.Read(mReadBuffer, 0, mReadBuffer.Length);
            availableToRead = mReadBufferLen;
        }

        if (count == 0)
            return 0;

        // We can satisfy the entire operation with what we have in the
        // read buffer, or we can't satisfy it but a new read would only trigger
        // one more read from the underlying stream (which is slow)
        if (count <= availableToRead + mReadBuffer.Length)
        {
            int countToReadFromBuffer = Math.Min(count, availableToRead);
            Buffer.BlockCopy(
                mReadBuffer,
                mReadBufferPos,
                buffer,
                offset,
                countToReadFromBuffer);

            mReadBufferPos += countToReadFromBuffer;
            return countToReadFromBuffer;
        }

        // We can not satisfy the entire operation with what we have in the
        // read buffer, and subsequent read operations would trigger more than
        // one read from the underlying stream (which is slow)
        Buffer.BlockCopy(
            mReadBuffer,
            mReadBufferPos,
            buffer,
            offset,
            availableToRead);

        int newOffset = offset + availableToRead;
        int newCount = count - availableToRead;

        mReadBufferPos = 0;
        mReadBufferLen = 0;

        return mInnerStream.Read(buffer, newOffset, newCount) + availableToRead;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Contract.Assert(mWriteBuffer is not null == mBufferingEnabled);

        Contract.Assert(buffer is not null);
        Contract.Assert(offset >= 0 && offset < buffer.Length);
        Contract.Assert(count >= 0);
        Contract.Assert(buffer.Length - offset < count);

        if (mWriteBuffer is null)
        {
            mInnerStream.Write(buffer, offset, count);
            return;
        }

        int spaceLeftInBuffer = mWriteBuffer.Length - mWriteBufferPos;
        
        // The entire input buffer fits into the write buffer
        if (count <= spaceLeftInBuffer)
        {
            Buffer.BlockCopy(
                buffer,
                offset,
                mWriteBuffer,
                mWriteBufferPos,
                count);

            mWriteBufferPos += count;
            return;
        }

        int remainingCount;

        // The input buffer doesn't fit into the write buffer,
        // but won't fill up the write buffer completely a second time
        // This means we only trigger one write to the underlying stream
        // (which we assume to be slow) and leave room in the write buffer
        // for future memory writes (which are fast!)
        if (count <= spaceLeftInBuffer + 0.9 * mWriteBuffer.Length)
        {
            Buffer.BlockCopy(
                buffer,
                offset,
                mWriteBuffer,
                mWriteBufferPos,
                spaceLeftInBuffer);
            
            remainingCount = count - spaceLeftInBuffer;
            mInnerStream.Write(mWriteBuffer, 0, mWriteBufferPos);
            mWriteBufferPos = 0;
            
            Buffer.BlockCopy(
                buffer,
                offset + spaceLeftInBuffer,
                mWriteBuffer,
                mWriteBufferPos,
                remainingCount);
            return;
        }
        
        // The input buffer doesn't fit into the write buffer,
        // and it WILL fill up the write buffer at least a second time, if not more
        // This means that we will trigger two write operations:
        //  - First one, with the content of the write buffer + anything else
        //    we can fit into it from the input buffer
        //   - Second one, with the rest of the input buffer
        Buffer.BlockCopy(
            buffer,
            offset,
            mWriteBuffer,
            mWriteBufferPos,
            spaceLeftInBuffer);

        remainingCount = count - spaceLeftInBuffer;
        mInnerStream.Write(mWriteBuffer, 0, mWriteBufferPos);
        mInnerStream.Write(buffer, offset + spaceLeftInBuffer, remainingCount);
        mWriteBufferPos = 0;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    readonly Stream mInnerStream;
    readonly bool mLeaveInnerStreamOpen;
    readonly ArrayPool<byte> mArrayPool;
    readonly int mBufferSize;

    byte[]? mReadBuffer;
    byte[]? mWriteBuffer;

    int mReadBufferLen = 0;
    int mReadBufferPos = 0;
    int mWriteBufferPos = 0;

    bool mBufferingEnabled;
}

using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;

namespace dotnetRpc.Core.Shared;

public class RpcBrotliStream : Stream
{
    public override bool CanRead => mBaseStream.CanRead;
    public override bool CanWrite => mBaseStream.CanWrite;

    public override bool CanSeek => false;

    public override long Length => throw new NotImplementedException();
    public override long Position
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public static int GetMaxCompressedLength(int inputSize)
        => BrotliEncoder.GetMaxCompressedLength(inputSize);

    public RpcBrotliStream(
        Stream baseStream,
        ArrayPool<byte> arrayPool,
        int maxChunkSize = 1 * 1024 * 1024)
    {
        mBaseStream = baseStream;
        mArrayPool = arrayPool;
        mMaxChunkSize = maxChunkSize;
        mCurrentReadChunk = new(arrayPool, maxChunkSize);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(new Span<byte>(buffer, offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        if (mCurrentReadChunk.HasPendingBytes)
        {
            ReadOnlySpan<byte> nextBlock = mCurrentReadChunk.GetNextBlock(buffer.Length);
            nextBlock.CopyTo(buffer);
            mCurrentReadChunk.ReturnBufferIfNeeded();
            return nextBlock.Length;
        }

        byte[] header = new byte[5];
        mBaseStream.ReadUntilCountFulfilled(header, 0, 1);

        CompressionHeader.Decode(header, out bool compressed, out byte sizeFlag);

        if (sizeFlag == CompressionHeader.SmallSizeFlag)
            mBaseStream.ReadUntilCountFulfilled(header, 1, 1);

        if (sizeFlag == CompressionHeader.RegularSizeFlag)
            mBaseStream.ReadUntilCountFulfilled(header, 1, 4);

        mCurrentReadChunk.FillFromStream(
            CompressionHeader.GetSize(header, sizeFlag),
            compressed,
            mBaseStream);

        return Read(buffer);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Write(new ReadOnlySpan<byte>(buffer, offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length > mMaxChunkSize)
        {
            for (int i = 0; i < buffer.Length; i += mMaxChunkSize)
            {
                ReadOnlySpan<byte> chunk = buffer.Slice(
                    start: i,
                    length: Math.Min(buffer.Length - i, mMaxChunkSize));

                Write(chunk);
            }

            return;
        }

        ReadOnlySpan<byte> header;
        byte[] compressionBuffer = mArrayPool.Rent(
            BrotliEncoder.GetMaxCompressedLength(buffer.Length));
        try
        {
            if (!BrotliEncoder.TryCompress(buffer, compressionBuffer, out int compressedLen)
                || compressedLen > buffer.Length)
            {
                header = CompressionHeader.Create(false, buffer.Length);
                WriteToBaseStream(header, buffer);
                return;
            }

            header = CompressionHeader.Create(true, compressedLen);
            WriteToBaseStream(header, new ReadOnlySpan<byte>(compressionBuffer, 0, compressedLen));
        }
        finally
        {
            mArrayPool.Return(compressionBuffer);
        }
    }

    void WriteToBaseStream(ReadOnlySpan<byte> header, ReadOnlySpan<byte> content)
    {
        int totalLength = header.Length + content.Length;

        byte[] buffer = mArrayPool.Rent(totalLength);
        try
        {
            Span<byte> headerDstSpan = new(buffer, 0, header.Length);
            Span<byte> contentDstSpan = new(buffer, header.Length, content.Length);

            header.CopyTo(headerDstSpan);
            content.CopyTo(contentDstSpan);

            mBaseStream.Write(buffer, 0, totalLength);
        }
        finally
        {
            mArrayPool.Return(buffer);
        }
    }

    public override void Flush() => mBaseStream.Flush();

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotImplementedException();

    public override void SetLength(long value)
        => throw new NotImplementedException();

    protected override void Dispose(bool disposing)
    {
        try
        {
            mBaseStream.Dispose();
        }
        finally
        {
            mCurrentReadChunk.Dispose();
        }
    }

    readonly CurrentReadChunk mCurrentReadChunk;
    readonly Stream mBaseStream;
    readonly int mMaxChunkSize;
    readonly ArrayPool<byte> mArrayPool;

    class CurrentReadChunk : IDisposable
    {
        internal bool HasPendingBytes => mLength > 0 && mRead < mLength;

        internal CurrentReadChunk(ArrayPool<byte> arrayPool, int maxChunkSize)
        {
            mArrayPool = arrayPool;
            mMaxChunkSize = maxChunkSize;
            mLength = 0;
            mRead = 0;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (mDisposed)
                return;

            if (disposing && mBuffer.Length > 0)
            {
                mArrayPool.Return(mBuffer);
                mLength = 0;
                mRead = 0;
            }

            mDisposed = true;
        }

        internal void FillFromStream(int compressedLen, bool compressed, Stream source)
        {
            if (mDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            byte[]? readBuffer = mArrayPool.Rent(compressedLen);
            byte[]? uncompressedBuffer = null;
            try
            {
                source.ReadUntilCountFulfilled(readBuffer, 0, compressedLen);
                if (!compressed)
                {
                    mBuffer = readBuffer;
                    readBuffer = null;
                    mLength = compressedLen;
                    mRead = 0;
                    return;
                }

                ReadOnlySpan<byte> sourceSpan = new(readBuffer, 0, compressedLen);
                uncompressedBuffer = mArrayPool.Rent(mMaxChunkSize);

                if (!BrotliDecoder.TryDecompress(
                    sourceSpan, uncompressedBuffer, out int uncompressedLen))
                {
                    throw new InvalidOperationException(
                        "Unable to decompress a compressed chunk using Brotli");
                }

                mBuffer = uncompressedBuffer;
                mLength = uncompressedLen;
                mRead = 0;
            }
            finally
            {
                if (readBuffer != null)
                    mArrayPool.Return(readBuffer);
            }
        }

        internal ReadOnlySpan<byte> GetNextBlock(int count)
        {
            if (mDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            int blockLength = Math.Min(count, mLength - mRead);
            ReadOnlySpan<byte> result = new(mBuffer, mRead, blockLength);
            mRead += blockLength;
            return result;
        }

        internal void ReturnBufferIfNeeded()
        {
            if (mDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            if (mRead == mLength && mBuffer.Length > 0)
            {
                mArrayPool.Return(mBuffer);
                mBuffer = Array.Empty<byte>();
                mLength = 0;
                mRead = 0;
            }
        }

        int mRead;
        int mLength;
        byte[] mBuffer = Array.Empty<byte>();
        private bool mDisposed;
        readonly ArrayPool<byte> mArrayPool;
        readonly int mMaxChunkSize;
    }
}

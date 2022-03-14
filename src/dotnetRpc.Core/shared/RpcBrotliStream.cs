using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;

namespace dotnetRpc.Core.Shared;

public class RpcBrotliStream : Stream
{
    public override bool CanRead => true;
    public override bool CanWrite => true;

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
        int maxChunkSize = 5 * 1024 * 1024)
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

    public override void Flush()
    {
        mBaseStream.Flush();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

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

    CurrentReadChunk mCurrentReadChunk;
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

        internal void FillFromStream(int size, bool compressed, Stream source)
        {
            if (mDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            byte[]? readBuffer = mArrayPool.Rent(size);
            byte[]? uncompressedBuffer = null;
            try
            {
                source.ReadUntilCountFulfilled(readBuffer, 0, size);
                if (!compressed)
                {
                    mBuffer = readBuffer;
                    readBuffer = null;
                    mLength = size;
                    mRead = 0;
                    return;
                }

                ReadOnlySpan<byte> sourceSpan = new(readBuffer, 0, size);
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

    static class CompressionHeader
    {
        public const int TinyMaxSize = 32;
        public const int SmallMaxSize = 8192;

        public const byte CompressedFlag = 0x80;
        public const byte UncompressedFlag = 0x00;

        public const byte TinySizeFlag = 0x00;
        public const byte SmallSizeFlag = 0x20;
        public const byte RegularSizeFlag = 0x40;

        public static void Decode(
            ReadOnlySpan<byte> header, out bool compressed, out byte sizeFlag)
        {
            byte zero = header[0];
            compressed = (zero & CompressedFlag) == CompressedFlag;
            sizeFlag = (byte)(zero & 0x60);
        }

        public static int GetSize(ReadOnlySpan<byte> header, byte sizeFlag) => 
            sizeFlag switch {
                TinySizeFlag => header[0] & 0x1F,
                SmallSizeFlag => ((header[0] & 0x1F) << 8) + header[1],
                RegularSizeFlag => (header[1] << 24) + (header[2] << 16) + (header[3] << 8) + header[4],
                _ => throw new ArgumentOutOfRangeException(nameof(sizeFlag))
            };

        public static ReadOnlySpan<byte> Create(bool compressed, int count)
        {
            byte[] result;

            if (count < TinyMaxSize)
            {
                result = new byte[1];

                result[0] = (byte)(compressed ? CompressedFlag : UncompressedFlag);
                result[0] += TinySizeFlag;
                result[0] += (byte)count;

                return result;
            }

            if (count < SmallMaxSize)
            {
                result = new byte[2];

                result[0] = (byte)(compressed ? CompressedFlag : UncompressedFlag);
                result[0] += SmallSizeFlag;
                result[0] += (byte)(count >> 8);
                result[1] = (byte)(count & 0x000000FF);

                return result;
            }

            result = new byte[5];

            result[0] = (byte)(compressed ? CompressedFlag : UncompressedFlag);
            result[0] += RegularSizeFlag;
            result[1] = (byte)((count & 0xFF000000) >> 24);
            result[2] = (byte)((count & 0x00FF0000) >> 16);
            result[3] = (byte)((count & 0x0000FF00) >> 8);
            result[4] = (byte)(count & 0x000000FF);

            return result;
        }
    }
}

using System;
using System.IO;
using System.Diagnostics;

namespace dotnetRpc.Shared;

public class MeteredStream : Stream
{
    public ulong ReadBytes => mReadBytes;
    public ulong WrittenBytes => mWrittenBytes;

    public TimeSpan ReadTime => mReadStopWatch.Elapsed;
    public TimeSpan WriteTime => mWriteStopWatch.Elapsed;

    public MeteredStream(Stream innerStream)
    {
        mInnerStream = innerStream;
    }

    public override bool CanRead => mInnerStream.CanRead;

    public override bool CanSeek => mInnerStream.CanSeek;

    public override bool CanWrite => mInnerStream.CanWrite;

    public override long Length => mInnerStream.Length;

    public override long Position
    {
        get => mInnerStream.Position;
        set => mInnerStream.Position = value;
    }

    public override void Flush()
    {
        mInnerStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        mReadStopWatch.Start();

        int read = mInnerStream.Read(buffer, offset, count);
        mReadBytes += (ulong)read;

        mReadStopWatch.Stop();

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return mInnerStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        mInnerStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        mWriteStopWatch.Start();

        mInnerStream.Write(buffer, offset, count);
        mWrittenBytes += (ulong)count;

        mWriteStopWatch.Stop();
    }

    ulong mReadBytes = 0;
    ulong mWrittenBytes = 0;

    readonly Stream mInnerStream;
    readonly object mSyncLock = new();

    readonly Stopwatch mReadStopWatch = new();
    readonly Stopwatch mWriteStopWatch = new();
}

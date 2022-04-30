using System;
using System.IO;

namespace miloRPC.Tests;

// This stream mimics the behavior of a NetworkStream (or any other kind of stream
// in which the client reads faster than what the stream can provide / buffer)
public class ThrottledStream : Stream
{
    public override bool CanRead => mTargetStream.CanRead;

    public override bool CanSeek => mTargetStream.CanSeek;

    public override bool CanWrite => mTargetStream.CanWrite;

    public override long Length => mTargetStream.Length;

    public override long Position
    {
        get => mTargetStream.Position;
        set => mTargetStream.Position = value;
    }

    public ThrottledStream(Stream targetStream)
    {
        mTargetStream = targetStream;
        mRandom = new(Environment.TickCount);
    }

    public override void Flush()
        => mTargetStream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
        => mTargetStream.Read(buffer, offset, mRandom.Next(1, count));

    public override long Seek(long offset, SeekOrigin origin)
        => mTargetStream.Seek(offset, origin);

    public override void SetLength(long value)
        => mTargetStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
        => mTargetStream.Write(buffer, offset, count);

    readonly Stream mTargetStream;
    readonly Random mRandom;
}

using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;

namespace miloRPC.Core.Channels;

public class MeteredStream : Stream
{
    public static MeteredStream MeteredNull => new(Null);

    public ulong ReadBytes { get; private set; }

    public ulong WrittenBytes { get; private set; }

    public TimeSpan ReadTime => mReadStopWatch.Elapsed;
    public TimeSpan WriteTime => mWriteStopWatch.Elapsed;

    public MeteredStream(Stream innerStream)
    {
        mInnerStream = innerStream;
    }

    public T GetInnerStream<T>() where T : Stream => Unsafe.As<T>(mInnerStream);

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
        ReadBytes += (ulong)read;

        mReadStopWatch.Stop();

        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken ct = new())
    {
        mReadStopWatch.Start();

        int read = await mInnerStream.ReadAsync(buffer, ct);
        ReadBytes += (ulong) read;

        mReadStopWatch.Stop();

        return read;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer, int offset, int count, CancellationToken ct)
    {
        mReadStopWatch.Start();

        int read = await mInnerStream.ReadAsync(buffer.AsMemory(offset, count), ct);
        ReadBytes += (ulong)read;

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
        WrittenBytes += (ulong)count;

        mWriteStopWatch.Stop();
    }

    public override async Task WriteAsync(
        byte[] buffer, int offset, int count, CancellationToken ct)
    {
        mWriteStopWatch.Start();

        await mInnerStream.WriteAsync(buffer.AsMemory(offset, count), ct);
        WrittenBytes += (ulong)count;

        mWriteStopWatch.Stop();
    }

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer, CancellationToken ct = new())
    {
        mWriteStopWatch.Start();

        await mInnerStream.WriteAsync(buffer, ct);
        WrittenBytes += (ulong)buffer.Length;

        mWriteStopWatch.Stop();
    }

    Stream mInnerStream;
    readonly Stopwatch mReadStopWatch = new();
    readonly Stopwatch mWriteStopWatch = new();
}

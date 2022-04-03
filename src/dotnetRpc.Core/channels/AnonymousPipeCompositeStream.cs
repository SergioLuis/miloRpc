using System;
using System.IO;
using System.IO.Pipes;

namespace dotnetRpc.Core.Channels;

public class AnonymousPipeCompositeStream : Stream
{
    public override bool CanRead => mInput.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => mOutput.CanWrite;

    public override long Length => throw new NotImplementedException();

    public override long Position
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public AnonymousPipeCompositeStream(
        AnonymousPipeServerStream output,
        AnonymousPipeClientStream input)
    {
        mOutput = output;
        mInput = input;
    }

    public override void Flush() => mOutput.Flush();

    public override int Read(byte[] buffer, int offset, int count)
        => mInput.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotImplementedException();


    public override void SetLength(long value)
        => throw new NotImplementedException();

    public override void Write(byte[] buffer, int offset, int count)
        => mOutput.Write(buffer, offset, count);

    readonly AnonymousPipeServerStream mOutput;
    readonly AnonymousPipeClientStream mInput;
}

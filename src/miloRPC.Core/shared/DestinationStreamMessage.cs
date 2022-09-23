using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;

namespace miloRPC.Core.Shared;

public class DestinationStreamMessage : INetworkMessage
{
    public Stream Stream
    {
        get
        {
            Contract.Assert(mStream is not null);
            return mStream;
        }
    }

    public DestinationStreamMessage(Action? disposeAction = null)
    {
        mDisposeAction = disposeAction;
    }

    public void Serialize(BinaryWriter writer)
        => throw new InvalidOperationException(
            "DestinationStreamMessage is only supposed to be used " +
            "to deserialize streams from the network");

    public void Deserialize(BinaryReader reader)
    {
        long length = reader.ReadInt64();
        mStream = new CappedNetworkStream(reader.BaseStream, length, mDisposeAction);
    }

    Stream? mStream;
    readonly Action? mDisposeAction;

    internal class CappedNetworkStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => mLength;

        public override long Position
        {
            get => mPosition;
            set => throw new NotSupportedException();
        }
        
        internal List<Action> DisposeActions { get; }

        internal CappedNetworkStream(
            Stream networkStream, long cappedLength, Action? disposeAction)
        {
            mPosition = 0;
            mNetworkStream = networkStream;
            mLength = cappedLength;

            DisposeActions = new List<Action>();
            if (disposeAction is not null)
                DisposeActions.Add(disposeAction);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Action disposeAction in DisposeActions)
                    disposeAction();
            }

            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = mNetworkStream.Read(buffer, offset, count);
            mPosition += read;
            return read;
        }
        
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Flush()
            => throw new NotSupportedException();

        int mPosition;
        readonly Stream mNetworkStream;
        readonly long mLength;
    }
}

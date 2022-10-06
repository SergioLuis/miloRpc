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

    public virtual void Deserialize(BinaryReader reader)
    {
        long length = reader.Read7BitEncodedInt64();
        Contract.Assert(length > 0);
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
        
        internal List<Action> SuccessfulDisposeActions { get; }
        internal Action? FailedDisposeAction { get; set; }

        internal CappedNetworkStream(
            Stream networkStream, long cappedLength, Action? disposeAction)
        {
            mPosition = 0;
            mNetworkStream = networkStream;
            mLength = cappedLength;

            SuccessfulDisposeActions = new List<Action>();
            if (disposeAction is not null)
                SuccessfulDisposeActions.Add(disposeAction);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing || mDisposed)
            {
                base.Dispose(disposing);
                return;
            }

            if (mPosition != Length)
            {
                FailedDisposeAction?.Invoke();
                throw new StreamNotConsumedRpcException(
                    "Stream is disposed but not completely consumed!");
            }

            foreach (Action disposeAction in SuccessfulDisposeActions)
                disposeAction();

            mDisposed = true;
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

        bool mDisposed;
    }
}

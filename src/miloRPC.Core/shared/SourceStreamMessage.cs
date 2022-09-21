using System;
using System.IO;

namespace miloRPC.Core.Shared;

public class SourceStreamMessage : INetworkMessage, IDisposable
{
    public SourceStreamMessage(Stream stream, Action? disposeAction = null)
    {
        mStream = stream;
        mDisposeAction = disposeAction;
    }

    readonly Stream mStream;
    readonly Action? mDisposeAction;

    public virtual void Serialize(BinaryWriter writer)
    {
        writer.Write((long)mStream.Length);
        mStream.CopyTo(writer.BaseStream);
    }

    public void Deserialize(BinaryReader reader)
        => throw new InvalidOperationException(
            "SourceStreamMessage is only supposed to be used " +
            "to serialize streams to the network");

    void IDisposable.Dispose()
    {
        if (mDisposeAction is not null)
            mDisposeAction.Invoke();
        else
            mStream.Dispose();

        GC.SuppressFinalize(this);
    }
}

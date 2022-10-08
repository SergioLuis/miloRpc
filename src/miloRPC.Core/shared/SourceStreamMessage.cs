using System;
using System.Diagnostics.Contracts;
using System.IO;

namespace miloRPC.Core.Shared;

public class SourceStreamMessage : INetworkMessage, IDisposable
{
    public Stream? Stream { get; set; }

    public SourceStreamMessage(Action? disposeAction = null)
    {
        mDisposeAction = disposeAction;
    }

    public virtual void Serialize(BinaryWriter writer)
    {
        Contract.Assert(Stream is not null);
        writer.Write7BitEncodedInt64((long)Stream.Length);
        Stream.CopyTo(writer.BaseStream);
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
            Stream?.Dispose();

        GC.SuppressFinalize(this);
    }

    readonly Action? mDisposeAction;
}

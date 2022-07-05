using System;
using System.Diagnostics.Contracts;
using System.IO;

using miloRPC.Core.Shared;

namespace miloRpc.TestWorkBench.Rpc.Shared;

public class ByteArrayMessage : INetworkMessage, IDisposable
{
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public int Length { get; set; } = 0;
    
    public ByteArrayMessage() { }

    public ByteArrayMessage(byte[] bytes, int length)
    {
        Bytes = bytes;
        Length = length;
    }

    public void SetDisposedCallback(Action act)
    {
        mDisposeAction = act;
    }

    void IDisposable.Dispose() => mDisposeAction?.Invoke();

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Length);
        writer.Write(Bytes, 0, Length);
    }

    public void Deserialize(BinaryReader reader)
    {
        Length = reader.ReadInt32();
        Contract.Assert(Bytes.Length >= Length);

        int read = 0;
        while (read < Length)
            read += reader.Read(Bytes, read, Length - read);
    }

    Action? mDisposeAction;
}

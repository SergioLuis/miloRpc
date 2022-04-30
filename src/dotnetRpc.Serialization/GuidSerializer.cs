using System;
using System.IO;

namespace dotnetRpc.Serialization;

public class GuidSerializer : ISerializer<Guid>
{
    Guid ISerializer<Guid>.Deserialize(BinaryReader reader)
    {
        byte[] buffer = new byte[16];
        reader.Read(buffer);
        return new(buffer);
    }

    void ISerializer<Guid>.Serialize(BinaryWriter writer, Guid t)
        => writer.Write(t.ToByteArray());
}

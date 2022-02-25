using System.IO;

namespace dotnetRpc.Shared.Serialization;

public class Int64Serializer : ISerializer<long>
{
    long ISerializer<long>.Deserialize(BinaryReader reader)
        => reader.ReadInt64();

    void ISerializer<long>.Serialize(BinaryWriter writer, long t)
        => writer.Write((long)t);
}

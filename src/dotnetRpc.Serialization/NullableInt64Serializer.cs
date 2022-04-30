using System.IO;

namespace dotnetRpc.Serialization;

public class NullableInt64Serializer : ISerializer<long?>
{
    long? ISerializer<long?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableInt64();

    void ISerializer<long?>.Serialize(BinaryWriter writer, long? t)
        => writer.WriteNullable(t);
}

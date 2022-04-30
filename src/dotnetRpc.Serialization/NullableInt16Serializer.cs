using System.IO;

namespace dotnetRpc.Serialization;

public class NullableInt16Serializer : ISerializer<short?>
{
    short? ISerializer<short?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableInt16();

    void ISerializer<short?>.Serialize(BinaryWriter writer, short? t)
        => writer.WriteNullable(t);
}

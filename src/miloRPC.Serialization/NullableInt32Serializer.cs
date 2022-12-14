using System.IO;

namespace miloRPC.Serialization;

public class NullableInt32Serializer : ISerializer<int?>
{
    int? ISerializer<int?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableInt32();

    void ISerializer<int?>.Serialize(BinaryWriter writer, int? t)
        => writer.WriteNullable(t);
}

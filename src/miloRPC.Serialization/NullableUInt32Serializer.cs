using System.IO;

namespace miloRPC.Serialization;

public class NullableUInt32Serializer : ISerializer<uint?>
{
    uint? ISerializer<uint?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableUInt32();

    void ISerializer<uint?>.Serialize(BinaryWriter writer, uint? t)
        => writer.WriteNullable(t);
}

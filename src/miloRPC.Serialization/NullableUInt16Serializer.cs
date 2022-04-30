using System.IO;

namespace miloRPC.Serialization;

public class NullableUInt16Serializer : ISerializer<ushort?>
{
    ushort? ISerializer<ushort?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableUInt16();

    void ISerializer<ushort?>.Serialize(BinaryWriter writer, ushort? t)
        => writer.WriteNullable(t);
}

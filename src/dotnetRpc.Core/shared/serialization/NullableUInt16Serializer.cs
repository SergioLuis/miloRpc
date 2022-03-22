using System.IO;

namespace dotnetRpc.Core.Shared.Serialization;

public class NullableUInt16Serializer : ISerializer<ushort?>
{
    ushort? ISerializer<ushort?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableUInt16();

    void ISerializer<ushort?>.Serialize(BinaryWriter writer, ushort? t)
        => writer.WriteNullable(t);
}

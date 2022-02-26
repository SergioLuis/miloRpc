using System.IO;

namespace dotnetRpc.Core.Shared.Serialization;

public class NullableUInt16Serializer : ISerializer<ushort?>
{
    ushort? ISerializer<ushort?>.Deserialize(BinaryReader reader)
        => reader.ReadBoolean() ? reader.ReadUInt16() : null;

    void ISerializer<ushort?>.Serialize(BinaryWriter writer, ushort? t)
    {
        writer.Write((bool)(t is not null));
        if (t is not null) writer.Write((ushort)(t.Value));
    }
}

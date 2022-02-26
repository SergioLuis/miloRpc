using System.IO;

namespace dotnetRpc.Shared.Serialization;

public class NullableUInt32Serializer : ISerializer<uint?>
{
    uint? ISerializer<uint?>.Deserialize(BinaryReader reader)
        => reader.ReadBoolean() ? reader.ReadUInt32() : null;

    void ISerializer<uint?>.Serialize(BinaryWriter writer, uint? t)
    {
        writer.Write((bool)(t is not null));
        if (t is not null) writer.Write((uint)(t.Value));
    }
}

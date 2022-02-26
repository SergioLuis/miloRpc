using System.IO;

namespace dotnetRpc.Shared.Serialization;

public class NullableUInt64Serializer : ISerializer<ulong?>
{
    ulong? ISerializer<ulong?>.Deserialize(BinaryReader reader)
        => reader.ReadBoolean() ? reader.ReadUInt64() : null;

    void ISerializer<ulong?>.Serialize(BinaryWriter writer, ulong? t)
    {
        writer.Write((bool)(t is not null));
        if (t is not null) writer.Write((ulong)(t.Value));
    }
}

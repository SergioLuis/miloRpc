using System.IO;

namespace dotnetRpc.Shared.Serialization;

public class NullableInt32Serializer : ISerializer<int?>
{
    int? ISerializer<int?>.Deserialize(BinaryReader reader)
        => reader.ReadBoolean() ? reader.ReadInt32() : null;

    void ISerializer<int?>.Serialize(BinaryWriter writer, int? t)
    {
        writer.Write((bool)(t is not null));
        if (t is not null) writer.Write((int)(t.Value));
    }
}

using System.IO;

namespace dotnetRpc.Core.Shared.Serialization;

public class NullableInt16Serializer : ISerializer<short?>
{
    short? ISerializer<short?>.Deserialize(BinaryReader reader)
        => reader.ReadBoolean() ? reader.ReadInt16() : null;

    void ISerializer<short?>.Serialize(BinaryWriter writer, short? t)
    {
        writer.Write((bool)(t is not null));
        if (t is not null) writer.Write((short)(t.Value));
    }
}

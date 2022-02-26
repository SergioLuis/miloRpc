using System.IO;

namespace dotnetRpc.Shared.Serialization;

public class NullableInt64Serializer : ISerializer<long?>
{
    long? ISerializer<long?>.Deserialize(BinaryReader reader)
        => reader.ReadBoolean() ? reader.ReadInt64() : null;

    void ISerializer<long?>.Serialize(BinaryWriter writer, long? t)
    {
        writer.Write((bool)(t is not null));
        if (t is not null) writer.Write((long)(t.Value));
    }
}

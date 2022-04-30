using System.IO;

namespace dotnetRpc.Serialization;

public class NullableSingleSerializer : ISerializer<float?>
{
    void ISerializer<float?>.Serialize(BinaryWriter writer, float? t)
        => writer.WriteNullable(t);

    float? ISerializer<float?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableSingle();
}

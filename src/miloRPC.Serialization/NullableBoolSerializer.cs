using System.IO;

namespace miloRPC.Serialization;

public class NullableBoolSerializer : ISerializer<bool?>
{
    bool? ISerializer<bool?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableBoolean();


    void ISerializer<bool?>.Serialize(BinaryWriter writer, bool? t)
        => writer.WriteNullable(t);
}

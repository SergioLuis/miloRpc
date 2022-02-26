using System.IO;

namespace dotnetRpc.Shared.Serialization;

public class NullableBoolSerializer : ISerializer<bool?>
{
    bool? ISerializer<bool?>.Deserialize(BinaryReader reader)
        => reader.ReadBoolean() ? reader.ReadBoolean() : null;
        

    void ISerializer<bool?>.Serialize(BinaryWriter writer, bool? t)
    {
        writer.Write((bool)(t is not null));
        if (t is not null) writer.Write((bool)(t.Value));
    }
}

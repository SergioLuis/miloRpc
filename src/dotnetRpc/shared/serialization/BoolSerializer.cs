using System.IO;

namespace dotnetRpc.Shared.Serialization;

public class BoolSerializer : ISerializer<bool>
{
    bool ISerializer<bool>.Deserialize(BinaryReader reader)
        => reader.ReadBoolean();

    void ISerializer<bool>.Serialize(BinaryWriter writer, bool t)
        => writer.Write((bool)t);
}

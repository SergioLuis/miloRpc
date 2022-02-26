using System.IO;

namespace dotnetRpc.Core.Shared.Serialization;

public class StringSerializer : ISerializer<string?>
{
    string? ISerializer<string?>.Deserialize(BinaryReader reader)
    {
        if (reader.ReadBoolean())
            return null;

        return reader.ReadString();
    }

    void ISerializer<string?>.Serialize(BinaryWriter writer, string? t)
    {
        writer.Write((bool)(t is null));
        if (t is not null)
            writer.Write((string)t);
    }
}

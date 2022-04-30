using System.IO;

namespace dotnetRpc.Serialization;

public class StringSerializer : ISerializer<string?>
{
    string? ISerializer<string?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableString();

    void ISerializer<string?>.Serialize(BinaryWriter writer, string? t)
        => writer.WriteNullable(t);
}

using System.IO;

namespace dotnetRpc.Core.Shared.Serialization;

public class NullableCharSerializer : ISerializer<char?>
{
    char? ISerializer<char?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableChar();

    void ISerializer<char?>.Serialize(BinaryWriter writer, char? t)
        => writer.WriteNullable(t);
}

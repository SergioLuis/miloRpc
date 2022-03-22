using System.IO;

namespace dotnetRpc.Core.Shared.Serialization;

public class CharSerializer : ISerializer<char>
{
    char ISerializer<char>.Deserialize(BinaryReader reader)
        => reader.ReadChar();

    void ISerializer<char>.Serialize(BinaryWriter writer, char t)
        => writer.Write((char)t);
}

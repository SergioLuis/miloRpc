using System.IO;

namespace miloRPC.Serialization;

public class CharSerializer : ISerializer<char>
{
    char ISerializer<char>.Deserialize(BinaryReader reader)
        => reader.ReadChar();

    void ISerializer<char>.Serialize(BinaryWriter writer, char t)
        => writer.Write((char)t);
}

using System.IO;

namespace miloRPC.Serialization;

public class ByteSerializer : ISerializer<byte>
{
    void ISerializer<byte>.Serialize(BinaryWriter writer, byte t)
        => writer.Write((byte)t);

    byte ISerializer<byte>.Deserialize(BinaryReader reader)
        => reader.ReadByte();
}

using System.IO;

namespace dotnetRpc.Shared.Serialization;

public class ByteSerializer : ISerializer<byte>
{
    byte ISerializer<byte>.Deserialize(BinaryReader reader)
        => reader.ReadByte();

    void ISerializer<byte>.Serialize(BinaryWriter writer, byte t)
        => writer.Write((byte)t);
}

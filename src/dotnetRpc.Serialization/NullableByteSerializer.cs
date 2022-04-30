using System.IO;

namespace dotnetRpc.Serialization;

public class NullableByteSerializer : ISerializer<byte?>
{
    byte? ISerializer<byte?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableByte();


    void ISerializer<byte?>.Serialize(BinaryWriter writer, byte? t)
        => writer.WriteNullable(t);
}

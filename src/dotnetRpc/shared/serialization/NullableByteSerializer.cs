using System.IO;

namespace dotnetRpc.Shared.Serialization;

public class NullableByteSerializer : ISerializer<byte?>
{
    byte? ISerializer<byte?>.Deserialize(BinaryReader reader)
        => reader.ReadBoolean() ? reader.ReadByte() : null;
        

    void ISerializer<byte?>.Serialize(BinaryWriter writer, byte? t)
    {
        writer.Write((bool)(t is not null));
        if (t is not null)
            writer.Write((byte)(t.Value));
    }
}

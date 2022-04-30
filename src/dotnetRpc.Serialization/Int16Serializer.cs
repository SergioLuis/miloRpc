using System.IO;

namespace dotnetRpc.Serialization;

public class Int16Serializer : ISerializer<short>
{
    short ISerializer<short>.Deserialize(BinaryReader reader)
        => reader.ReadInt16();

    void ISerializer<short>.Serialize(BinaryWriter writer, short t)
        => writer.Write((short)t);
}

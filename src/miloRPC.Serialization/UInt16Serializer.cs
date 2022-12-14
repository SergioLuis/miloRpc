using System.IO;

namespace miloRPC.Serialization;

public class UInt16Serializer : ISerializer<ushort>
{
    ushort ISerializer<ushort>.Deserialize(BinaryReader reader)
        => reader.ReadUInt16();

    void ISerializer<ushort>.Serialize(BinaryWriter writer, ushort t)
        => writer.Write((ushort)t);
}

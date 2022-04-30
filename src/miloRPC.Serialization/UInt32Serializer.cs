using System.IO;

namespace miloRPC.Serialization;

public class UInt32Serializer : ISerializer<uint>
{
    uint ISerializer<uint>.Deserialize(BinaryReader reader)
        => reader.ReadUInt32();

    void ISerializer<uint>.Serialize(BinaryWriter writer, uint t)
        => writer.Write((uint)t);
}

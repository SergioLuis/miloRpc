using System.IO;

namespace dotnetRpc.Shared.Serialization;

public class UInt64Serializer : ISerializer<ulong>
{
    ulong ISerializer<ulong>.Deserialize(BinaryReader reader)
        => reader.ReadUInt64();

    void ISerializer<ulong>.Serialize(BinaryWriter writer, ulong t)
        => writer.Write((ulong)t);
}

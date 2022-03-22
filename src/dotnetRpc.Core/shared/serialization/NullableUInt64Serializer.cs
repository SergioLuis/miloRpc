using System.IO;

namespace dotnetRpc.Core.Shared.Serialization;

public class NullableUInt64Serializer : ISerializer<ulong?>
{
    ulong? ISerializer<ulong?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableUInt64();

    void ISerializer<ulong?>.Serialize(BinaryWriter writer, ulong? t)
        => writer.WriteNullable(t);
}

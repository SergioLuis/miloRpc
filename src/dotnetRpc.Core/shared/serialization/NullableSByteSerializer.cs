using System.IO;

namespace dotnetRpc.Core.Shared.Serialization;

public class NullableSByteSerializer : ISerializer<sbyte?>
{
    sbyte? ISerializer<sbyte?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableSByte();

    void ISerializer<sbyte?>.Serialize(BinaryWriter writer, sbyte? t)
        => writer.WriteNullable(t);
}

using System.IO;

namespace dotnetRpc.Core.Shared.Serialization;

public class NullableDoubleSerializer : ISerializer<double?>
{
    double? ISerializer<double?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableDouble();

    void ISerializer<double?>.Serialize(BinaryWriter writer, double? t)
        => writer.WriteNullable(t);
}

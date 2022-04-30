using System.IO;

namespace miloRPC.Serialization;

public class NullableDecimalSerializers : ISerializer<decimal?>
{
    decimal? ISerializer<decimal?>.Deserialize(BinaryReader reader)
        => reader.ReadNullableDecimal();

    void ISerializer<decimal?>.Serialize(BinaryWriter writer, decimal? t)
        => writer.WriteNullable(t);
}

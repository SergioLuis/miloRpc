using System.IO;

namespace miloRPC.Serialization;

public class DecimalSerializer : ISerializer<decimal>
{
    decimal ISerializer<decimal>.Deserialize(BinaryReader reader)
        => reader.ReadDecimal();

    void ISerializer<decimal>.Serialize(BinaryWriter writer, decimal t)
        => writer.Write((decimal) t);
}

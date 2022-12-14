using System.IO;

namespace miloRPC.Serialization;

public class DoubleSerializer : ISerializer<double>
{
    double ISerializer<double>.Deserialize(BinaryReader reader)
        => reader.ReadDouble();

    void ISerializer<double>.Serialize(BinaryWriter writer, double t)
        => writer.Write((double) t);
}

using System.IO;

namespace dotnetRpc.Serialization;

public class SingleSerializer : ISerializer<float>
{
    void ISerializer<float>.Serialize(BinaryWriter writer, float t)
        => writer.Write(t);

    float ISerializer<float>.Deserialize(BinaryReader reader)
        => reader.ReadSingle();
}

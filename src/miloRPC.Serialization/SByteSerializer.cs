using System.IO;

namespace miloRPC.Serialization;

public class SByteSerializer : ISerializer<sbyte>
{
    sbyte ISerializer<sbyte>.Deserialize(BinaryReader reader)
        => reader.ReadSByte();

    void ISerializer<sbyte>.Serialize(BinaryWriter writer, sbyte t)
        => writer.Write((sbyte) t);
}

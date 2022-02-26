using System.IO;

namespace dotnetRpc.Core.Shared.Serialization;

public class Int32Serialier : ISerializer<int>
{
    int ISerializer<int>.Deserialize(BinaryReader reader)
        => reader.ReadInt32();

    void ISerializer<int>.Serialize(BinaryWriter writer, int t)
        => writer.Write((int)t);
}

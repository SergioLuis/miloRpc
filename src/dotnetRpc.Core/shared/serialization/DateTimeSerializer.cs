using System;
using System.IO;

namespace dotnetRpc.Core.Shared.Serialization;

public class DateTimeSerializer : ISerializer<DateTime>
{
    DateTime ISerializer<DateTime>.Deserialize(BinaryReader reader)
        => DateTime.FromFileTimeUtc(reader.ReadInt64());

    void ISerializer<DateTime>.Serialize(BinaryWriter writer, DateTime t)
        => writer.Write((long)t.ToFileTimeUtc());
}

using System.Collections.Generic;
using System.IO;

namespace miloRPC.Serialization;

public class ListSerializer<T> : ISerializer<List<T?>>
{
    public ListSerializer()
    {
        mIntSerializer = Serializer<int>.Instance;
        mInnerSerializer = Serializer<T>.Instance;
    }

    void ISerializer<List<T?>>.Serialize(BinaryWriter writer, List<T?>? t)
    {
        writer.Write(t is not null);
        if (t is null)
            return;

        mIntSerializer.Serialize(writer, t.Count);
        foreach (T? item in t)
            mInnerSerializer.Serialize(writer, item);
    }

    List<T?>? ISerializer<List<T?>>.Deserialize(BinaryReader reader)
    {
        if (!reader.ReadBoolean())
            return null;

        int count = mIntSerializer.Deserialize(reader);
        List<T?> result = new(count);

        for (int i = 0; i < count; i++)
            result.Add(mInnerSerializer.Deserialize(reader));

        return result;
    }

    readonly ISerializer<int> mIntSerializer;
    readonly ISerializer<T> mInnerSerializer;
}

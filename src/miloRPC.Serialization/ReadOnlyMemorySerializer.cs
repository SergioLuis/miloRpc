using System;
using System.IO;

namespace miloRPC.Serialization;

public class ReadOnlyMemorySerializer<T> : ISerializer<ReadOnlyMemory<T?>>
{
    public ReadOnlyMemorySerializer()
    {
        mIntSerializer = Serializer<int>.Instance;
        mInnerSerializer = Serializer<T>.Instance;
    }

    void ISerializer<ReadOnlyMemory<T?>>.Serialize(BinaryWriter writer, ReadOnlyMemory<T?> t)
    {
        ReadOnlySpan<T?> span = t.Span;
        
        mIntSerializer.Serialize(writer, span.Length);
        foreach (var item in span)
            mInnerSerializer.Serialize(writer, item);
    }

    ReadOnlyMemory<T?> ISerializer<ReadOnlyMemory<T?>>.Deserialize(BinaryReader reader)
    {
        int count = mIntSerializer.Deserialize(reader);
        T?[] result = new T[count];

        for (int i = 0; i < count; i++)
            result[i] = mInnerSerializer.Deserialize(reader);

        return result;
    }

    readonly ISerializer<int> mIntSerializer;
    readonly ISerializer<T> mInnerSerializer;
}

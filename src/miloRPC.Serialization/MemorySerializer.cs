using System;
using System.IO;

namespace miloRPC.Serialization;

public class MemorySerializer<T> : ISerializer<Memory<T?>>
{
    public MemorySerializer()
    {
        mIntSerializer = Serializer<int>.Instance;
        mInnerSerializer = Serializer<T>.Instance;
    }

    void ISerializer<Memory<T?>>.Serialize(BinaryWriter writer, Memory<T?> t)
    {
        Span<T?> span = t.Span;
        
        mIntSerializer.Serialize(writer, span.Length);
        foreach (var item in span)
            mInnerSerializer.Serialize(writer, item);
    }

    Memory<T?> ISerializer<Memory<T?>>.Deserialize(BinaryReader reader)
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

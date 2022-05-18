using System;
using System.IO;

namespace miloRPC.Serialization;

public class TupleSerializer<T1, T2> : ISerializer<Tuple<T1?, T2?>>
{
    public TupleSerializer()
    {
        mItem1Serializer = Serializer<T1>.Instance;
        mItem2Serializer = Serializer<T2>.Instance;
    }

    void ISerializer<Tuple<T1?, T2?>>.Serialize(BinaryWriter writer, Tuple<T1?, T2?>? t)
    {
        writer.Write(t is not null);
        if (t is null)
            return;

        mItem1Serializer.Serialize(writer, t.Item1);
        mItem2Serializer.Serialize(writer, t.Item2);
    }

    Tuple<T1?, T2?>? ISerializer<Tuple<T1?, T2?>>.Deserialize(BinaryReader reader)
    {
        if (!reader.ReadBoolean())
            return null;

        return new Tuple<T1?, T2?>(
            mItem1Serializer.Deserialize(reader),
            mItem2Serializer.Deserialize(reader));
    }

    readonly ISerializer<T1> mItem1Serializer;
    readonly ISerializer<T2> mItem2Serializer;
}

public class TupleSerializer<T1, T2, T3> : ISerializer<Tuple<T1?, T2?, T3?>>
{
    public TupleSerializer()
    {
        mItem1Serializer = Serializer<T1>.Instance;
        mItem2Serializer = Serializer<T2>.Instance;
        mItem3Serializer = Serializer<T3>.Instance;
    }

    void ISerializer<Tuple<T1?, T2?, T3?>>.Serialize(BinaryWriter writer, Tuple<T1?, T2?, T3?>? t)
    {
        writer.Write(t is not null);
        if (t is null)
            return;

        mItem1Serializer.Serialize(writer, t.Item1);
        mItem2Serializer.Serialize(writer, t.Item2);
        mItem3Serializer.Serialize(writer, t.Item3);
    }

    Tuple<T1?, T2?, T3?>? ISerializer<Tuple<T1?, T2?, T3?>>.Deserialize(BinaryReader reader)
    {
        if (!reader.ReadBoolean())
            return null;

        return new Tuple<T1?, T2?, T3?>(
            mItem1Serializer.Deserialize(reader),
            mItem2Serializer.Deserialize(reader),
            mItem3Serializer.Deserialize(reader));
    }

    readonly ISerializer<T1> mItem1Serializer;
    readonly ISerializer<T2> mItem2Serializer;
    readonly ISerializer<T3> mItem3Serializer;
}

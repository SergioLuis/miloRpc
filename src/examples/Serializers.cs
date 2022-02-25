using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

using dotnetRpc.Shared;

namespace dotnetRpc.Examples;

public interface ISerializer { }

public interface ISerializer<T> : ISerializer
{
    void Serialize(BinaryWriter writer, T? t);
    T? Deserialize(BinaryReader reader);
}

public class StringSerializer : ISerializer<string?>
{
    string? ISerializer<string?>.Deserialize(BinaryReader reader)
    {
        if (reader.ReadBoolean())
            return null;

        return reader.ReadString();
    }

    void ISerializer<string?>.Serialize(BinaryWriter writer, string? t)
    {
        writer.Write((bool)(t is null));
        if (t is not null)
            writer.Write((string)t);
    }
}

public class Int32Serialier : ISerializer<int>
{
    int ISerializer<int>.Deserialize(BinaryReader reader)
        => reader.ReadInt32();

    void ISerializer<int>.Serialize(BinaryWriter writer, int t)
        => writer.Write((int)t);
}

public class Int16Serializer : ISerializer<short>
{
    short ISerializer<short>.Deserialize(BinaryReader reader)
        => reader.ReadInt16();

    void ISerializer<short>.Serialize(BinaryWriter writer, short t)
        => writer.Write((short)t);
}

public class Int64Serializer : ISerializer<long>
{
    long ISerializer<long>.Deserialize(BinaryReader reader)
        => reader.ReadInt64();

    void ISerializer<long>.Serialize(BinaryWriter writer, long t)
        => writer.Write((long)t);
}

internal class Serializers
{
    internal Serializers()
    {
        mSerializers = new Dictionary<Type, ISerializer>();
        AddSerializer(new StringSerializer());
        AddSerializer(new Int16Serializer());
        AddSerializer(new Int32Serialier());
        AddSerializer(new Int64Serializer());
    }

    public void AddSerializer<T>(ISerializer<T> serializer)
    {
        mSerializers.Add(typeof(T), serializer);
    }

    public ISerializer<T> Get<T>()
    {
        Type t = typeof(T);
        if (mSerializers.TryGetValue(t, out ISerializer? serializer))
            return Unsafe.As<ISerializer<T>>(serializer);

        throw new InvalidOperationException($"Can't find a serializer for type {t}");
    }

    readonly Dictionary<Type, ISerializer> mSerializers;

    public static readonly Serializers Instance = new();
}

public class NetworkMessage<T> : INetworkMessage
{
    public T? Val { get; set; } = default(T);

    public void Deserialize(BinaryReader reader)
    {
        Val = Serializers.Instance.Get<T>().Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        Serializers.Instance.Get<T>().Serialize(writer, Val);
    }
}

public class NetworkMessage<T1, T2> : INetworkMessage
{
    public T1? Val1 { get; set; } = default(T1);
    public T2? Val2 { get; set; } = default(T2);

    public void Deserialize(BinaryReader reader)
    {
        Val1 = Serializers.Instance.Get<T1>().Deserialize(reader);
        Val2 = Serializers.Instance.Get<T2>().Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        Serializers.Instance.Get<T1>().Serialize(writer, Val1);
        Serializers.Instance.Get<T2>().Serialize(writer, Val2);
    }
}

public class NetworkMessage<T1, T2, T3> : INetworkMessage
{
    public T1? Val1 { get; set; } = default(T1);
    public T2? Val2 { get; set; } = default(T2);
    public T3? Val3 { get; set; } = default(T3);

    public void Deserialize(BinaryReader reader)
    {
        Val1 = Serializers.Instance.Get<T1>().Deserialize(reader);
        Val2 = Serializers.Instance.Get<T2>().Deserialize(reader);
        Val3 = Serializers.Instance.Get<T3>().Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        Serializers.Instance.Get<T1>().Serialize(writer, Val1);
        Serializers.Instance.Get<T2>().Serialize(writer, Val2);
        Serializers.Instance.Get<T3>().Serialize(writer, Val3);
    }
}

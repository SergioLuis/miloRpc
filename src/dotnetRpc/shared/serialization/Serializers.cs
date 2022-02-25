using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace dotnetRpc.Shared.Serialization;

public interface ISerializer { }

public interface ISerializer<T> : ISerializer
{
    void Serialize(BinaryWriter writer, T? t);
    T? Deserialize(BinaryReader reader);
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
            return Unsafe.As<ISerializer<T>>(serializer)!;

        throw new InvalidOperationException($"Can't find a serializer for type {t}");
    }

    readonly Dictionary<Type, ISerializer> mSerializers;

    public static readonly Serializers Instance = new();
}

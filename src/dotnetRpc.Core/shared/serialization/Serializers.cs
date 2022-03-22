using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace dotnetRpc.Core.Shared.Serialization;

public interface ISerializer { }

public interface ISerializer<T> : ISerializer
{
    void Serialize(BinaryWriter writer, T? t);
    T? Deserialize(BinaryReader reader);
}

public static class Serializer<T>
{
    public static void Serialize(BinaryWriter writer, T? value)
        => Instance.Serialize(writer, value);

    public static T? Deserialize(BinaryReader reader)
        => Instance.Deserialize(reader);

    static ISerializer<T> Instance
    {
        get
        {
            if (_initialized)
                return _instance!;

            lock (mSyncLock)
            {
                _instance ??= Serializers.Instance.Get<T>();
            }

            _initialized = true;
            return _instance;
        }
    }

    static volatile bool _initialized;
    static ISerializer<T>? _instance;
    static readonly object mSyncLock = new();
}

internal class Serializers
{
    Serializers()
    {
        mSerializers = new Dictionary<Type, ISerializer>();
        AddSerializer(new StringSerializer());

        AddSerializer(new ByteSerializer());
        AddSerializer(new NullableByteSerializer());
        AddSerializer(new BoolSerializer());
        AddSerializer(new NullableBoolSerializer());
        AddSerializer(new CharSerializer());
        AddSerializer(new NullableCharSerializer());

        AddSerializer(new DecimalSerializer());
        AddSerializer(new NullableDecimalSerializers());
        AddSerializer(new Int16Serializer());
        AddSerializer(new NullableInt16Serializer());
        AddSerializer(new Int32Serialier());
        AddSerializer(new NullableInt32Serializer());
        AddSerializer(new Int64Serializer());
        AddSerializer(new NullableInt64Serializer());

        AddSerializer(new SingleSerializer());
        AddSerializer(new NullableSingleSerializer());
        AddSerializer(new DoubleSerializer());
        AddSerializer(new NullableDoubleSerializer());
        AddSerializer(new SByteSerializer());
        AddSerializer(new NullableSByteSerializer());

        AddSerializer(new UInt16Serializer());
        AddSerializer(new NullableUInt16Serializer());
        AddSerializer(new UInt32Serializer());
        AddSerializer(new NullableUInt32Serializer());
        AddSerializer(new UInt64Serializer());
        AddSerializer(new NullableUInt64Serializer());

        AddSerializer(new DateTimeSerializer());
        AddSerializer(new GuidSerializer());
    }

    public void AddSerializer<T>(ISerializer<T> serializer)
        => mSerializers.Add(typeof(T), serializer);

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

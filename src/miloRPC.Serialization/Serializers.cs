using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace miloRPC.Serialization;

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

            lock (_syncLock)
            {
                _instance ??= Serializers.Instance.Get<T>();
            }

            _initialized = true;
            return _instance;
        }
    }

    static volatile bool _initialized;
    static ISerializer<T>? _instance;
    static readonly object _syncLock = new();
}

public class Serializers
{
    Serializers()
    {
        mSerializers = new Dictionary<Type, ISerializer>();
        Add(new StringSerializer());

        Add(new ByteSerializer());
        Add(new NullableByteSerializer());
        Add(new BoolSerializer());
        Add(new NullableBoolSerializer());
        Add(new CharSerializer());
        Add(new NullableCharSerializer());

        Add(new DecimalSerializer());
        Add(new NullableDecimalSerializers());
        Add(new Int16Serializer());
        Add(new NullableInt16Serializer());
        Add(new Int32Serialier());
        Add(new NullableInt32Serializer());
        Add(new Int64Serializer());
        Add(new NullableInt64Serializer());

        Add(new SingleSerializer());
        Add(new NullableSingleSerializer());
        Add(new DoubleSerializer());
        Add(new NullableDoubleSerializer());
        Add(new SByteSerializer());
        Add(new NullableSByteSerializer());

        Add(new UInt16Serializer());
        Add(new NullableUInt16Serializer());
        Add(new UInt32Serializer());
        Add(new NullableUInt32Serializer());
        Add(new UInt64Serializer());
        Add(new NullableUInt64Serializer());

        Add(new DateTimeSerializer());
        Add(new GuidSerializer());
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public void Add<T>(ISerializer<T> serializer)
        => mSerializers.Add(typeof(T), serializer);

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

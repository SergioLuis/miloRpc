using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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

    public static ISerializer<T> Instance
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
        mSerializers = new ConcurrentDictionary<Type, ISerializer>();
        mGenericSerializers = new ConcurrentDictionary<Type, Type>();

        RegisterInstance(new StringSerializer());

        RegisterInstance(new ByteSerializer());
        RegisterInstance(new NullableByteSerializer());
        RegisterInstance(new BoolSerializer());
        RegisterInstance(new NullableBoolSerializer());
        RegisterInstance(new CharSerializer());
        RegisterInstance(new NullableCharSerializer());

        RegisterInstance(new DecimalSerializer());
        RegisterInstance(new NullableDecimalSerializers());
        RegisterInstance(new Int16Serializer());
        RegisterInstance(new NullableInt16Serializer());
        RegisterInstance(new Int32Serialier());
        RegisterInstance(new NullableInt32Serializer());
        RegisterInstance(new Int64Serializer());
        RegisterInstance(new NullableInt64Serializer());

        RegisterInstance(new SingleSerializer());
        RegisterInstance(new NullableSingleSerializer());
        RegisterInstance(new DoubleSerializer());
        RegisterInstance(new NullableDoubleSerializer());
        RegisterInstance(new SByteSerializer());
        RegisterInstance(new NullableSByteSerializer());

        RegisterInstance(new UInt16Serializer());
        RegisterInstance(new NullableUInt16Serializer());
        RegisterInstance(new UInt32Serializer());
        RegisterInstance(new NullableUInt32Serializer());
        RegisterInstance(new UInt64Serializer());
        RegisterInstance(new NullableUInt64Serializer());

        RegisterInstance(new DateTimeSerializer());
        RegisterInstance(new GuidSerializer());

        RegisterGeneric(typeof(Memory<>), typeof(MemorySerializer<>));
        RegisterGeneric(typeof(ReadOnlyMemory<>), typeof(ReadOnlyMemorySerializer<>));
        RegisterGeneric(typeof(List<>), typeof(ListSerializer<>));
        RegisterGeneric(typeof(Tuple<,>), typeof(TupleSerializer<,>));
        RegisterGeneric(typeof(Tuple<,,>), typeof(TupleSerializer<,,>));
    }

    public void RegisterInstance<T>(ISerializer<T> serializer)
        => mSerializers[typeof(T)] = serializer;

    public void RegisterGeneric(Type genericTargetType, Type genericSerializerType)
        => mGenericSerializers[genericTargetType] = genericSerializerType;

    public ISerializer<T> Get<T>()
    {
        Type t = typeof(T);
        if (mSerializers.TryGetValue(t, out ISerializer? serializer))
            return Unsafe.As<ISerializer<T>>(serializer);

        if (t.IsGenericType && t.IsConstructedGenericType)
        {
            // Let's try to build a serializer!
            Type targetType = t.GetGenericTypeDefinition();

            if (!mGenericSerializers.TryGetValue(targetType, out var serializerType))
                throw new InvalidOperationException($"Can't find a serializer for type {t}");

            Type resultType = serializerType.MakeGenericType(t.GenericTypeArguments);

            if (Activator.CreateInstance(resultType) is ISerializer<T> result)
            {
                mSerializers[t] = result;
                return result;
            }
        }

        throw new InvalidOperationException($"Can't find a serializer for type {t}");
    }

    readonly ConcurrentDictionary<Type, ISerializer> mSerializers;
    readonly ConcurrentDictionary<Type, Type> mGenericSerializers;

    public static readonly Serializers Instance = new();
}

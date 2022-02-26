using System.IO;

namespace dotnetRpc.Shared.Serialization;

public class NetworkMessage<T> : INetworkMessage
{
    public T? Val1 { get; set; }

    public void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T>.Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        Serializer<T>.Serialize(writer, Val1);
    }
}

public class NetworkMessage<T1, T2> : INetworkMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }

    public void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T1>.Deserialize(reader);
        Val2 = Serializer<T2>.Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        Serializer<T1>.Serialize(writer, Val1);
        Serializer<T2>.Serialize(writer, Val2);
    }
}

public class NetworkMessage<T1, T2, T3> : INetworkMessage
{
    public T1? Val1 { get; set; } = default(T1);
    public T2? Val2 { get; set; } = default(T2);
    public T3? Val3 { get; set; } = default(T3);

    public void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T1>.Deserialize(reader);
        Val2 = Serializer<T2>.Deserialize(reader);
        Val3 = Serializer<T3>.Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        Serializer<T1>.Serialize(writer, Val1);
        Serializer<T2>.Serialize(writer, Val2);
        Serializer<T3>.Serialize(writer, Val3);
    }
}

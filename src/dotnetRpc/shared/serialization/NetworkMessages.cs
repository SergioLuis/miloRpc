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
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }

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

public class NetworkMessage<T1, T2, T3, T4> : INetworkMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }

    public void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T1>.Deserialize(reader);
        Val2 = Serializer<T2>.Deserialize(reader);
        Val3 = Serializer<T3>.Deserialize(reader);
        Val4 = Serializer<T4>.Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        Serializer<T1>.Serialize(writer, Val1);
        Serializer<T2>.Serialize(writer, Val2);
        Serializer<T3>.Serialize(writer, Val3);
        Serializer<T4>.Serialize(writer, Val4);
    }
}

public class NetworkMessage<T1, T2, T3, T4, T5> : INetworkMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }
    public T5? Val5 { get; set; }

    public void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T1>.Deserialize(reader);
        Val2 = Serializer<T2>.Deserialize(reader);
        Val3 = Serializer<T3>.Deserialize(reader);
        Val4 = Serializer<T4>.Deserialize(reader);
        Val5 = Serializer<T5>.Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        Serializer<T1>.Serialize(writer, Val1);
        Serializer<T2>.Serialize(writer, Val2);
        Serializer<T3>.Serialize(writer, Val3);
        Serializer<T4>.Serialize(writer, Val4);
        Serializer<T5>.Serialize(writer, Val5);
    }
}

public class NetworkMessage<T1, T2, T3, T4, T5, T6> : INetworkMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }
    public T5? Val5 { get; set; }
    public T6? Val6 { get; set; }

    public void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T1>.Deserialize(reader);
        Val2 = Serializer<T2>.Deserialize(reader);
        Val3 = Serializer<T3>.Deserialize(reader);
        Val4 = Serializer<T4>.Deserialize(reader);
        Val5 = Serializer<T5>.Deserialize(reader);
        Val6 = Serializer<T6>.Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        Serializer<T1>.Serialize(writer, Val1);
        Serializer<T2>.Serialize(writer, Val2);
        Serializer<T3>.Serialize(writer, Val3);
        Serializer<T4>.Serialize(writer, Val4);
        Serializer<T5>.Serialize(writer, Val5);
        Serializer<T6>.Serialize(writer, Val6);
    }
}

public class NetworkMessage<T1, T2, T3, T4, T5, T6, T7> : INetworkMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }
    public T5? Val5 { get; set; }
    public T6? Val6 { get; set; }
    public T7? Val7 { get; set; }

    public void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T1>.Deserialize(reader);
        Val2 = Serializer<T2>.Deserialize(reader);
        Val3 = Serializer<T3>.Deserialize(reader);
        Val4 = Serializer<T4>.Deserialize(reader);
        Val5 = Serializer<T5>.Deserialize(reader);
        Val6 = Serializer<T6>.Deserialize(reader);
        Val7 = Serializer<T7>.Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        Serializer<T1>.Serialize(writer, Val1);
        Serializer<T2>.Serialize(writer, Val2);
        Serializer<T3>.Serialize(writer, Val3);
        Serializer<T4>.Serialize(writer, Val4);
        Serializer<T5>.Serialize(writer, Val5);
        Serializer<T6>.Serialize(writer, Val6);
        Serializer<T7>.Serialize(writer, Val7);
    }
}

public class NetworkMessage<T1, T2, T3, T4, T5, T6, T7, T8> : INetworkMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }
    public T5? Val5 { get; set; }
    public T6? Val6 { get; set; }
    public T7? Val7 { get; set; }
    public T8? Val8 { get; set; }

    public void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T1>.Deserialize(reader);
        Val2 = Serializer<T2>.Deserialize(reader);
        Val3 = Serializer<T3>.Deserialize(reader);
        Val4 = Serializer<T4>.Deserialize(reader);
        Val5 = Serializer<T5>.Deserialize(reader);
        Val6 = Serializer<T6>.Deserialize(reader);
        Val7 = Serializer<T7>.Deserialize(reader);
        Val8 = Serializer<T8>.Deserialize(reader);
    }

    public void Serialize(BinaryWriter writer)
    {
        Serializer<T1>.Serialize(writer, Val1);
        Serializer<T2>.Serialize(writer, Val2);
        Serializer<T3>.Serialize(writer, Val3);
        Serializer<T4>.Serialize(writer, Val4);
        Serializer<T5>.Serialize(writer, Val5);
        Serializer<T6>.Serialize(writer, Val6);
        Serializer<T7>.Serialize(writer, Val7);
        Serializer<T8>.Serialize(writer, Val8);
    }
}

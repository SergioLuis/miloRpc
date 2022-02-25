using System.IO;

namespace dotnetRpc.Shared.Serialization;

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

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

using System;
using System.IO;

using miloRPC.Core.Shared;

namespace miloRPC.Serialization;

public class VoidNetworkMessage : INetworkMessage
{
    public void Deserialize(BinaryReader reader) => reader.ReadBoolean();
    public void Serialize(BinaryWriter writer) => writer.Write(true);
}

public class NetworkMessage<T> : INetworkMessage
{
    public T? Val1 { get; set; }

    public NetworkMessage() { }

    public NetworkMessage(T? val1)
    {
        Val1 = val1;
    }

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

    public NetworkMessage() { }

    public NetworkMessage(T1? val1, T2? val2)
    {
        Val1 = val1;
        Val2 = val2;
    }

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

    public NetworkMessage() { }

    public NetworkMessage(T1? val1, T2? val2, T3? val3)
    {
        Val1 = val1;
        Val2 = val2;
        Val3 = val3;
    }

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

    public NetworkMessage() { }

    public NetworkMessage(T1? val1, T2? val2, T3? val3, T4? val4)
    {
        Val1 = val1;
        Val2 = val2;
        Val3 = val3;
        Val4 = val4;
    }

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

    public NetworkMessage() { }

    public NetworkMessage(T1? val1, T2? val2, T3? val3, T4? val4, T5? val5)
    {
        Val1 = val1;
        Val2 = val2;
        Val3 = val3;
        Val4 = val4;
        Val5 = val5;
    }

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

    public NetworkMessage() { }

    public NetworkMessage(T1? val1, T2? val2, T3? val3, T4? val4, T5? val5, T6? val6)
    {
        Val1 = val1;
        Val2 = val2;
        Val3 = val3;
        Val4 = val4;
        Val5 = val5;
        Val6 = val6;
    }

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

    public NetworkMessage() { }

    public NetworkMessage(
        T1? val1, T2? val2, T3? val3, T4? val4, T5? val5, T6? val6, T7? val7)
    {
        Val1 = val1;
        Val2 = val2;
        Val3 = val3;
        Val4 = val4;
        Val5 = val5;
        Val6 = val6;
        Val7 = val7;
    }

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

    public NetworkMessage() { }

    public NetworkMessage(
        T1? val1, T2? val2, T3? val3, T4? val4, T5? val5, T6? val6, T7? val7, T8? val8)
    {
        Val1 = val1;
        Val2 = val2;
        Val3 = val3;
        Val4 = val4;
        Val5 = val5;
        Val6 = val6;
        Val7 = val7;
        Val8 = val8;
    }

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

public class SourceStreamMessage<T> : SourceStreamMessage
{
    public T? Val1 { get; set; }

    public SourceStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Serialize(BinaryWriter writer)
    {
        Serializer<T>.Serialize(writer, Val1);
        base.Serialize(writer);
    }
}

public class SourceStreamMessage<T1, T2> : SourceStreamMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }

    public SourceStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Serialize(BinaryWriter writer)
    {
        Serializer<T1>.Serialize(writer, Val1);
        Serializer<T2>.Serialize(writer, Val2);
        base.Serialize(writer);
    }
}

public class SourceStreamMessage<T1, T2, T3> : SourceStreamMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }

    public SourceStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Serialize(BinaryWriter writer)
    {
        Serializer<T1>.Serialize(writer, Val1);
        Serializer<T2>.Serialize(writer, Val2);
        Serializer<T3>.Serialize(writer, Val3);
        base.Serialize(writer);
    }
}

public class SourceStreamMessage<T1, T2, T3, T4> : SourceStreamMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }

    public SourceStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Serialize(BinaryWriter writer)
    {
        Serializer<T1>.Serialize(writer, Val1);
        Serializer<T2>.Serialize(writer, Val2);
        Serializer<T3>.Serialize(writer, Val3);
        Serializer<T4>.Serialize(writer, Val4);
        base.Serialize(writer);
    }
}

public class SourceStreamMessage<T1, T2, T3, T4, T5> : SourceStreamMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }
    public T5? Val5 { get; set; }

    public SourceStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Serialize(BinaryWriter writer)
    {
        Serializer<T1>.Serialize(writer, Val1);
        Serializer<T2>.Serialize(writer, Val2);
        Serializer<T3>.Serialize(writer, Val3);
        Serializer<T4>.Serialize(writer, Val4);
        Serializer<T5>.Serialize(writer, Val5);
        base.Serialize(writer);
    }
}

public class SourceStreamMessage<T1, T2, T3, T4, T5, T6> : SourceStreamMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }
    public T5? Val5 { get; set; }
    public T6? Val6 { get; set; }

    public SourceStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Serialize(BinaryWriter writer)
    {
        Serializer<T1>.Serialize(writer, Val1);
        Serializer<T2>.Serialize(writer, Val2);
        Serializer<T3>.Serialize(writer, Val3);
        Serializer<T4>.Serialize(writer, Val4);
        Serializer<T5>.Serialize(writer, Val5);
        Serializer<T6>.Serialize(writer, Val6);
        base.Serialize(writer);
    }
}

public class SourceStreamMessage<T1, T2, T3, T4, T5, T6, T7> : SourceStreamMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }
    public T5? Val5 { get; set; }
    public T6? Val6 { get; set; }
    public T7? Val7 { get; set; }

    public SourceStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Serialize(BinaryWriter writer)
    {
        Serializer<T1>.Serialize(writer, Val1);
        Serializer<T2>.Serialize(writer, Val2);
        Serializer<T3>.Serialize(writer, Val3);
        Serializer<T4>.Serialize(writer, Val4);
        Serializer<T5>.Serialize(writer, Val5);
        Serializer<T6>.Serialize(writer, Val6);
        Serializer<T7>.Serialize(writer, Val7);
        base.Serialize(writer);
    }
}

public class SourceStreamMessage<T1, T2, T3, T4, T5, T6, T7, T8> : SourceStreamMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }
    public T5? Val5 { get; set; }
    public T6? Val6 { get; set; }
    public T7? Val7 { get; set; }
    public T8? Val8 { get; set; }

    public SourceStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Serialize(BinaryWriter writer)
    {
        Serializer<T1>.Serialize(writer, Val1);
        Serializer<T2>.Serialize(writer, Val2);
        Serializer<T3>.Serialize(writer, Val3);
        Serializer<T4>.Serialize(writer, Val4);
        Serializer<T5>.Serialize(writer, Val5);
        Serializer<T6>.Serialize(writer, Val6);
        Serializer<T7>.Serialize(writer, Val7);
        Serializer<T8>.Serialize(writer, Val8);
        base.Serialize(writer);
    }
}

public class DestinationStreamMessage<T> : DestinationStreamMessage
{
    public T? Val1 { get; set; }
    
    public DestinationStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T>.Deserialize(reader);
        base.Deserialize(reader);
    }
}

public class DestinationStreamMessage<T1, T2> : DestinationStreamMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    
    public DestinationStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T1>.Deserialize(reader);
        Val2 = Serializer<T2>.Deserialize(reader);
        base.Deserialize(reader);
    }
}

public class DestinationStreamMessage<T1, T2, T3> : DestinationStreamMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    
    public DestinationStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T1>.Deserialize(reader);
        Val2 = Serializer<T2>.Deserialize(reader);
        Val3 = Serializer<T3>.Deserialize(reader);
        base.Deserialize(reader);
    }
}

public class DestinationStreamMessage<T1, T2, T3, T4> : DestinationStreamMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }
    
    public DestinationStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T1>.Deserialize(reader);
        Val2 = Serializer<T2>.Deserialize(reader);
        Val3 = Serializer<T3>.Deserialize(reader);
        Val4 = Serializer<T4>.Deserialize(reader);
        base.Deserialize(reader);
    }
}

public class DestinationStreamMessage<T1, T2, T3, T4, T5> : DestinationStreamMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }
    public T5? Val5 { get; set; }
    
    public DestinationStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T1>.Deserialize(reader);
        Val2 = Serializer<T2>.Deserialize(reader);
        Val3 = Serializer<T3>.Deserialize(reader);
        Val4 = Serializer<T4>.Deserialize(reader);
        Val5 = Serializer<T5>.Deserialize(reader);
        base.Deserialize(reader);
    }
}

public class DestinationStreamMessage<T1, T2, T3, T4, T5, T6> : DestinationStreamMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }
    public T5? Val5 { get; set; }
    public T6? Val6 { get; set; }
    
    public DestinationStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T1>.Deserialize(reader);
        Val2 = Serializer<T2>.Deserialize(reader);
        Val3 = Serializer<T3>.Deserialize(reader);
        Val4 = Serializer<T4>.Deserialize(reader);
        Val5 = Serializer<T5>.Deserialize(reader);
        Val6 = Serializer<T6>.Deserialize(reader);
        base.Deserialize(reader);
    }
}

public class DestinationStreamMessage<T1, T2, T3, T4, T5, T6, T7> : DestinationStreamMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }
    public T5? Val5 { get; set; }
    public T6? Val6 { get; set; }
    public T7? Val7 { get; set; }
    
    public DestinationStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T1>.Deserialize(reader);
        Val2 = Serializer<T2>.Deserialize(reader);
        Val3 = Serializer<T3>.Deserialize(reader);
        Val4 = Serializer<T4>.Deserialize(reader);
        Val5 = Serializer<T5>.Deserialize(reader);
        Val6 = Serializer<T6>.Deserialize(reader);
        Val7 = Serializer<T7>.Deserialize(reader);
        base.Deserialize(reader);
    }
}

public class DestinationStreamMessage<T1, T2, T3, T4, T5, T6, T7, T8> : DestinationStreamMessage
{
    public T1? Val1 { get; set; }
    public T2? Val2 { get; set; }
    public T3? Val3 { get; set; }
    public T4? Val4 { get; set; }
    public T5? Val5 { get; set; }
    public T6? Val6 { get; set; }
    public T7? Val7 { get; set; }
    public T8? Val8 { get; set; }
    
    public DestinationStreamMessage(Action? disposeAction = null) : base(disposeAction) { }

    public override void Deserialize(BinaryReader reader)
    {
        Val1 = Serializer<T1>.Deserialize(reader);
        Val2 = Serializer<T2>.Deserialize(reader);
        Val3 = Serializer<T3>.Deserialize(reader);
        Val4 = Serializer<T4>.Deserialize(reader);
        Val5 = Serializer<T5>.Deserialize(reader);
        Val6 = Serializer<T6>.Deserialize(reader);
        Val7 = Serializer<T7>.Deserialize(reader);
        Val8 = Serializer<T8>.Deserialize(reader);
        base.Deserialize(reader);
    }
}

using System;
using System.Diagnostics.Contracts;
using System.IO;
using miloRPC.Serialization;

namespace miloRPC.Examples.Shared;

public static class ExampleSerializers
{
    public static void RegisterSerializers()
    {
        Serializers.Instance.RegisterInstance(new EchoResultSerializer());
    }
}

public class EchoResultSerializer : ISerializer<EchoResult>
{
    public void Serialize(BinaryWriter writer, EchoResult? t)
    {
        writer.Write(t is not null);
        if (t is null) return;

        mDateTimeSerializer.Serialize(writer, t.ReceptionDateUtc);
        mStringSerializer.Serialize(writer, t.ReceivedMessage);
    }

    public EchoResult? Deserialize(BinaryReader reader)
    {
        if (!reader.ReadBoolean())
            return null;

        DateTime dt = mDateTimeSerializer.Deserialize(reader);
        string? str = mStringSerializer.Deserialize(reader);

        Contract.Assert(dt > DateTime.MinValue);
        Contract.Assert(str is not null);

        return new EchoResult(dt, str);
    }

    readonly ISerializer<DateTime> mDateTimeSerializer = Serializers.Instance.Get<DateTime>();
    readonly ISerializer<string> mStringSerializer = Serializers.Instance.Get<string>();
}

using System.IO;

using miloRPC.Core.Shared;

namespace miloRPC.Core.Server;

public interface IWriteMethodCallResult
{
    void Write(
        BinaryWriter writer, MethodCallResult result, SerializableException? ex = null);
}

public class DefaultWriteMethodCallResult : IWriteMethodCallResult
{
    void IWriteMethodCallResult.Write(
        BinaryWriter writer, MethodCallResult result, SerializableException? ex)
    {
        writer.Write((byte)result);
        writer.Write((bool)(ex is not null));

        if (ex is not null)
            SerializableException.ToWriter(ex, writer);
    }

    public static readonly IWriteMethodCallResult Instance =
        new DefaultWriteMethodCallResult();
}

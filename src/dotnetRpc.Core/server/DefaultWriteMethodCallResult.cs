using System.IO;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Server;

public interface IWriteMethodCallResult
{
    void Write(
        BinaryWriter writer, MethodCallResult result, RpcException? ex = null);
}

public class DefaultWriteMethodCallResult : IWriteMethodCallResult
{
    void IWriteMethodCallResult.Write(
        BinaryWriter writer, MethodCallResult result, RpcException? ex)
    {
        writer.Write((byte)result);
        writer.Write((bool)(ex is not null));

        if (ex is not null)
            RpcException.ToWriter(ex, writer);
    }

    public static readonly IWriteMethodCallResult Instance =
        new DefaultWriteMethodCallResult();
}

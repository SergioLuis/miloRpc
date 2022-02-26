using System;
using System.IO;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Server;

public interface IWriteMethodCallResult
{
    void Write(
        BinaryWriter writer, MethodCallResult result, Exception? ex = null);
}

public class DefaultWriteMethodCallResult : IWriteMethodCallResult
{
    void IWriteMethodCallResult.Write(
        BinaryWriter writer, MethodCallResult result, Exception? ex)
    {
        writer.Write((byte)result);
        writer.Write((bool)(ex is not null));
        
        if (ex is null)
            return;
        
        // TODO: Serialize the exception
    }

    public static readonly IWriteMethodCallResult Instance =
        new DefaultWriteMethodCallResult();
}

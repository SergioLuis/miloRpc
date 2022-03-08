using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

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

        WriteException(writer, ex);
    }

    static void WriteException(BinaryWriter writer, Exception ex)
    {
        BinaryFormatter formatter = new();
        using MemoryStream ms = new();

#pragma warning disable
        formatter.Serialize(ms, ex);
#pragma warning restore

        writer.Write((int)ms.Length);
        ms.Position = 0;
        ms.CopyTo(writer.BaseStream);
    }

    public static readonly IWriteMethodCallResult Instance =
        new DefaultWriteMethodCallResult();
}

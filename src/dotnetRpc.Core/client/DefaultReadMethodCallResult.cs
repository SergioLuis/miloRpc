using System;
using System.Buffers;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Client;

public interface IReadMethodCallResult
{
    MethodCallResult Read(
        BinaryReader reader,
        out bool isResultAvailable,
        out Exception? ex);
}

public class DefaultReadMethodCallResult : IReadMethodCallResult
{
    MethodCallResult IReadMethodCallResult.Read(
        BinaryReader reader, out bool isResultAvailable, out Exception? ex)
    {
        MethodCallResult methodResult = (MethodCallResult)(reader.ReadByte());
        bool isExceptionAvailable = reader.ReadBoolean();

        if (isExceptionAvailable)
        {
            isResultAvailable = false;
            ex = ReadException(reader);
            return methodResult;
        }

        isResultAvailable = true;
        ex = null;
        return methodResult;
    }

    static Exception ReadException(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        byte[] buffer = ArrayPool<byte>.Shared.Rent((int)length);
        try
        {
            reader.Read(buffer, 0, length);
            using MemoryStream ms = new(buffer);
            BinaryFormatter formatter = new();

#pragma warning disable
            return (Exception)formatter.Deserialize(ms);
#pragma warning restore
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static readonly IReadMethodCallResult Instance =
        new DefaultReadMethodCallResult();
}

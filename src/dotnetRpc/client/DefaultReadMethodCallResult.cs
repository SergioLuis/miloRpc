using System;
using System.IO;

using dotnetRpc.Shared;

namespace dotnetRpc.Client;

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
            // TODO: deserialize exception
            isResultAvailable = false;
            ex = null;
            return methodResult;
        }

        isResultAvailable = true;
        ex = null;
        return methodResult;
    }

    public static readonly IReadMethodCallResult Instance =
        new DefaultReadMethodCallResult();
}

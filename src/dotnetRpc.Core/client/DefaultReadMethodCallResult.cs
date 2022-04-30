using System.IO;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Client;

public interface IReadMethodCallResult
{
    MethodCallResult Read(
        BinaryReader reader,
        out bool isResultAvailable,
        out RpcException? ex);
}

public class DefaultReadMethodCallResult : IReadMethodCallResult
{
    MethodCallResult IReadMethodCallResult.Read(
        BinaryReader reader, out bool isResultAvailable, out RpcException? ex)
    {
        MethodCallResult methodResult = (MethodCallResult)(reader.ReadByte());
        bool isExceptionAvailable = reader.ReadBoolean();

        isResultAvailable = methodResult == MethodCallResult.Ok;
        ex = isExceptionAvailable ? RpcException.FromReader(reader) : null;

        return methodResult;
    }

    public static readonly IReadMethodCallResult Instance =
        new DefaultReadMethodCallResult();
}

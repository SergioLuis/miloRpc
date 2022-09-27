using System.IO;

using miloRPC.Core.Shared;

namespace miloRPC.Core.Client;

public interface IReadMethodCallResult
{
    MethodCallResult Read(
        BinaryReader reader,
        out bool isResultAvailable,
        out SerializableException? ex);
}

public class DefaultReadMethodCallResult : IReadMethodCallResult
{
    MethodCallResult IReadMethodCallResult.Read(
        BinaryReader reader, out bool isResultAvailable, out SerializableException? ex)
    {
        MethodCallResult methodResult = (MethodCallResult)(reader.ReadByte());
        bool isExceptionAvailable = reader.ReadBoolean();

        isResultAvailable = methodResult == MethodCallResult.Ok;
        ex = isExceptionAvailable ? SerializableException.FromReader(reader) : null;

        return methodResult;
    }

    public static readonly IReadMethodCallResult Instance =
        new DefaultReadMethodCallResult();
}

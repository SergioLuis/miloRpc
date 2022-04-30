using System;
using System.IO;

using dotnetRpc.Core.Shared.Serialization;

namespace dotnetRpc.Core.Shared;

public class RpcException : Exception
{
    public string? ExceptionType => mExceptionType;
    public override string Message => mExceptionMessage;
    public override string? StackTrace => mStackTrace;

    public static RpcException FromException(Exception ex)
        => new RpcException(ex.GetType().FullName, ex.Message, ex.StackTrace);

    private RpcException(
        string? originalExceptionType,
        string originalExceptionMessage,
        string? originalStackTrace)
    {
        mExceptionType = originalExceptionType;
        mExceptionMessage = originalExceptionMessage;
        mStackTrace = originalStackTrace;
    }

    private RpcException()
    {
        mExceptionType = string.Empty;
        mExceptionMessage = string.Empty;
        mStackTrace = string.Empty;
    }

    internal static RpcException FromReader(BinaryReader reader)
    {
        RpcException result = new();
        result.Deserialize(reader);
        return result;
    }

    internal static void ToWriter(RpcException exception, BinaryWriter writer)
        => exception.Serialize(writer);

    string? mExceptionType;
    string mExceptionMessage;
    string? mStackTrace;

    void Serialize(BinaryWriter writer)
    {
        Serializer<string>.Serialize(writer, mExceptionType);
        Serializer<string>.Serialize(writer, mExceptionMessage);
        Serializer<string>.Serialize(writer, mStackTrace);
    }

    void Deserialize(BinaryReader reader)
    {
        mExceptionType = Serializer<string>.Deserialize(reader) ?? string.Empty;
        mExceptionMessage = Serializer<string>.Deserialize(reader) ?? string.Empty;
        mStackTrace = Serializer<string>.Deserialize(reader);
    }
}

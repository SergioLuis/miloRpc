using System;
using System.IO;

namespace dotnetRpc.Core.Shared;

public class RpcException : Exception
{
    public string? ExceptionType => mExceptionType;
    public override string Message => mExceptionMessage;
    public override string? StackTrace => mStackTrace;

    public static RpcException FromException(Exception ex)
        => new(ex.GetType().FullName, ex.Message, ex.StackTrace);

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
        SerializeNullable(writer, mExceptionType);
        writer.Write((string)mExceptionMessage);
        SerializeNullable(writer, mStackTrace);
    }

    void Deserialize(BinaryReader reader)
    {
        mExceptionType = DeserializeNullable(reader);
        mExceptionMessage = reader.ReadString();
        mStackTrace = DeserializeNullable(reader);
    }

    static void SerializeNullable(BinaryWriter writer, string? str)
    {
        writer.Write((bool)(str is not null));
        if (str is not null)
            writer.Write(str);
    }

    static string? DeserializeNullable(BinaryReader reader)
    {
        return reader.ReadBoolean() ? reader.ReadString() : null;
    }
}

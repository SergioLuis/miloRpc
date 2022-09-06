using System;
using System.IO;

namespace miloRPC.Core.Shared;

public class SerializableException : Exception
{
    public string? ExceptionType => mExceptionType;
    public override string Message => mExceptionMessage;
    public override string? StackTrace => mStackTrace;

    public static SerializableException FromException(Exception ex)
        => new(ex.GetType().FullName, ex.Message, ex.StackTrace);

    private SerializableException(
        string? originalExceptionType,
        string originalExceptionMessage,
        string? originalStackTrace)
    {
        mExceptionType = originalExceptionType;
        mExceptionMessage = originalExceptionMessage;
        mStackTrace = originalStackTrace;
    }

    private SerializableException()
    {
        mExceptionType = string.Empty;
        mExceptionMessage = string.Empty;
        mStackTrace = string.Empty;
    }

    internal static SerializableException FromReader(BinaryReader reader)
    {
        SerializableException result = new();
        result.Deserialize(reader);
        return result;
    }

    internal static void ToWriter(SerializableException exception, BinaryWriter writer)
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

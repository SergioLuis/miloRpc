using System.IO;

namespace dotnetRpc.Serialization;

public static class BinaryWriterExtensionMethods
{
    public static void WriteNullable(this BinaryWriter writer, bool? obj)
    {
        writer.Write(obj.HasValue);
        if (obj.HasValue) writer.Write(obj.Value);
    }

    public static void WriteNullable(this BinaryWriter writer, byte? obj)
    {
        writer.Write(obj.HasValue);
        if (obj.HasValue) writer.Write(obj.Value);
    }

    public static void WriteNullable(this BinaryWriter writer, char? obj)
    {
        writer.Write(obj.HasValue);
        if (obj.HasValue) writer.Write(obj.Value);
    }

    public static void WriteNullable(this BinaryWriter writer, decimal? obj)
    {
        writer.Write(obj.HasValue);
        if (obj.HasValue) writer.Write(obj.Value);
    }

    public static void WriteNullable(this BinaryWriter writer, double? obj)
    {
        writer.Write(obj.HasValue);
        if (obj.HasValue) writer.Write(obj.Value);
    }

    public static void WriteNullable(this BinaryWriter writer, float? obj)
    {
        writer.Write(obj.HasValue);
        if (obj.HasValue) writer.Write(obj.Value);
    }

    public static void WriteNullable(this BinaryWriter writer, int? obj)
    {
        writer.Write(obj.HasValue);
        if (obj.HasValue) writer.Write(obj.Value);
    }

    public static void WriteNullable(this BinaryWriter writer, long? obj)
    {
        writer.Write(obj.HasValue);
        if (obj.HasValue) writer.Write(obj.Value);
    }

    public static void WriteNullable(this BinaryWriter writer, sbyte? obj)
    {
        writer.Write(obj.HasValue);
        if (obj.HasValue) writer.Write(obj.Value);
    }

    public static void WriteNullable(this BinaryWriter writer, short? obj)
    {
        writer.Write(obj.HasValue);
        if (obj.HasValue) writer.Write(obj.Value);
    }

    public static void WriteNullable(this BinaryWriter writer, string? obj)
    {
        writer.Write(obj is not null);
        if (obj is not null) writer.Write(obj);
    }

    public static void WriteNullable(this BinaryWriter writer, uint? obj)
    {
        writer.Write(obj.HasValue);
        if (obj.HasValue) writer.Write(obj.Value);
    }

    public static void WriteNullable(this BinaryWriter writer, ulong? obj)
    {
        writer.Write(obj.HasValue);
        if (obj.HasValue) writer.Write(obj.Value);
    }

    public static void WriteNullable(this BinaryWriter writer, ushort? obj)
    {
        writer.Write(obj.HasValue);
        if (obj.HasValue) writer.Write(obj.Value);
    }
}

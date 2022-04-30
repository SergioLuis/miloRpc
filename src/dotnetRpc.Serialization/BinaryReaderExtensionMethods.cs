using System;
using System.IO;

namespace dotnetRpc.Serialization;

public static class BinaryReaderExtensionMethods
{
    public static bool? ReadNullableBoolean(this BinaryReader reader)
        => ReadStruct(reader, r => r.ReadBoolean());

    public static byte? ReadNullableByte(this BinaryReader reader)
        => ReadStruct(reader, r => r.ReadByte());

    public static char? ReadNullableChar(this BinaryReader reader)
        => ReadStruct(reader, r => r.ReadChar());

    public static decimal? ReadNullableDecimal(this BinaryReader reader)
        => ReadStruct(reader, r => r.ReadDecimal());

    public static double? ReadNullableDouble(this BinaryReader reader)
        => ReadStruct(reader, r => r.ReadDouble());

    public static float? ReadNullableSingle(this BinaryReader reader)
        => ReadStruct(reader, r => r.ReadSingle());

    public static int? ReadNullableInt32(this BinaryReader reader)
        => ReadStruct(reader, r => r.ReadInt32());

    public static long? ReadNullableInt64(this BinaryReader reader)
        => ReadStruct(reader, r => r.ReadInt64());

    public static sbyte? ReadNullableSByte(this BinaryReader reader)
        => ReadStruct(reader, r => r.ReadSByte());

    public static short? ReadNullableInt16(this BinaryReader reader)
        => ReadStruct(reader, r => r.ReadInt16());

    public static string? ReadNullableString(this BinaryReader reader)
        => ReadRef(reader, r => r.ReadString());

    public static ulong? ReadNullableUInt64(this BinaryReader reader)
        => ReadStruct(reader, r => r.ReadUInt64());

    public static uint? ReadNullableUInt32(this BinaryReader reader)
        => ReadStruct(reader, r => r.ReadUInt32());

    public static ushort? ReadNullableUInt16(this BinaryReader reader)
        => ReadStruct(reader, r => r.ReadUInt16());

    static T? ReadStruct<T>(BinaryReader reader, Func<BinaryReader, T> readAction) where T : struct
        => reader.ReadBoolean() switch
        {
            true => readAction(reader),
            false => null
        };

    static T? ReadRef<T>(BinaryReader reader, Func<BinaryReader, T> readAction) where T : class
        => reader.ReadBoolean() switch
        {
            true => readAction(reader),
            false => null
        };
}

using System;

namespace dotnetRpc.Core.Shared;

public static class CompressionHeader
{
    public const int TinyMaxSize = 32;
    public const int SmallMaxSize = 8192;

    public const byte CompressedFlag = 0x80;
    public const byte UncompressedFlag = 0x00;

    public const byte TinySizeFlag = 0x20;
    public const byte SmallSizeFlag = 0x40;
    public const byte RegularSizeFlag = 0x60;

    public static void Decode(
        ReadOnlySpan<byte> header, out bool compressed, out byte sizeFlag)
    {
        byte zero = header[0];
        compressed = (zero & CompressedFlag) == CompressedFlag;
        sizeFlag = (byte)(zero & 0x60);
    }

    public static string HeaderToString(ReadOnlySpan<byte> header)
    {
        string result = string.Empty;
        for (int i = 0; i < header.Length; i++)
        {
            result = string.Concat(result, header[i].ToString("X"));
        }

        return result;
    }

    public static int GetSize(ReadOnlySpan<byte> header, byte sizeFlag) =>
        sizeFlag switch
        {
            TinySizeFlag => header[0] & 0x1F,
            SmallSizeFlag => ((header[0] & 0x1F) << 8) + header[1],
            RegularSizeFlag => (header[1] << 24) + (header[2] << 16) + (header[3] << 8) + header[4],
            _ => throw new ArgumentOutOfRangeException(nameof(sizeFlag))
        };

    public static ReadOnlySpan<byte> Create(bool compressed, int count)
    {
        byte[] result;

        if (count < TinyMaxSize)
        {
            result = new byte[1];

            result[0] = (byte)(compressed ? CompressedFlag : UncompressedFlag);
            result[0] += TinySizeFlag;
            result[0] += (byte)count;

            return result;
        }

        if (count < SmallMaxSize)
        {
            result = new byte[2];

            result[0] = (byte)(compressed ? CompressedFlag : UncompressedFlag);
            result[0] += SmallSizeFlag;
            result[0] += (byte)(count >> 8);
            result[1] = (byte)(count & 0x000000FF);

            return result;
        }

        result = new byte[5];

        result[0] = (byte)(compressed ? CompressedFlag : UncompressedFlag);
        result[0] += RegularSizeFlag;
        result[1] = (byte)((count & 0xFF000000) >> 24);
        result[2] = (byte)((count & 0x00FF0000) >> 16);
        result[3] = (byte)((count & 0x0000FF00) >> 8);
        result[4] = (byte)(count & 0x000000FF);

        return result;
    }
}

using System;

namespace miloRPC.Core.Shared;

public static class CompressionHeader
{
    public const int TinyHeaderSize = 1;
    public const int SmallHeaderSize = 2;
    public const int RegularHeaderSize = 5;

    public enum SizeFlag : byte
    {
        Tiny = 0x20,
        Small = 0x40,
        Regular = 0x60
    }

    public enum CompressionFlag : byte
    {
        Uncompressed = 0x00,
        Compressed = 0x80
    }

    public static void Decode(
        ReadOnlySpan<byte> header, out CompressionFlag compressed, out SizeFlag sizeFlag)
    {
        byte zero = header[0];
        compressed = (CompressionFlag) (zero & (byte) CompressionFlag.Compressed);
        sizeFlag = (SizeFlag)(zero & 0x60);
    }

    public static int GetSize(ReadOnlySpan<byte> header, SizeFlag sizeFlag) =>
        sizeFlag switch
        {
            SizeFlag.Tiny => header[0] & 0x1F,
            SizeFlag.Small => ((header[0] & 0x1F) << 8) + header[1],
            SizeFlag.Regular => (header[1] << 24) + (header[2] << 16) + (header[3] << 8) + header[4],
            _ => throw new ArgumentOutOfRangeException(nameof(sizeFlag))
        };

    public static ReadOnlySpan<byte> Create(CompressionFlag compressed, int count)
    {
        byte[] result;
        switch (count)
        {
            case < TinyMaxSize:
                result = new byte[1];

                result[0] = (byte)compressed;
                result[0] += (byte)SizeFlag.Tiny;
                result[0] += (byte)count;

                return result;

            case < SmallMaxSize:
                result = new byte[2];

                result[0] = (byte)compressed;
                result[0] += (byte)SizeFlag.Small;
                result[0] += (byte)(count >> 8);
                result[1] = (byte)(count & 0x000000FF);

                return result;

            default:
                result = new byte[5];

                result[0] = (byte)compressed;
                result[0] += (byte)SizeFlag.Regular;
                result[1] = (byte)((count & 0xFF000000) >> 24);
                result[2] = (byte)((count & 0x00FF0000) >> 16);
                result[3] = (byte)((count & 0x0000FF00) >> 8);
                result[4] = (byte)(count & 0x000000FF);

                return result;
        }
    }

    const int TinyMaxSize = 32;
    const int SmallMaxSize = 8192;
}

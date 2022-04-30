using System;
using System.IO;

namespace miloRPC.Core.Shared;

public static class StreamExtensionMethods
{
    public static void ReadUntilCountFulfilled(
        this Stream st, Span<byte> destination)
    {
        int read = 0;
        while (read < destination.Length)
        {
            int nextBlock = st.Read(destination.Slice(read));
            if (nextBlock == 0)
                throw new IOException("No more bytes to consume");
            read += nextBlock;
        }
    }

    public static void ReadUntilCountFulfilled(
        this Stream st, byte[] buffer, int offset, int count)
    {
        int read = 0;
        while (read < count)
        {
            int nextBlock = st.Read(buffer, offset + read, count - read);
            if (nextBlock == 0)
                throw new IOException("No more bytes to consume");
            read += nextBlock;
        }
    }
}

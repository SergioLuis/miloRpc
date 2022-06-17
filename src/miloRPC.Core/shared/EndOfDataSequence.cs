using System.IO;
using System.Security.Cryptography;

namespace miloRPC.Core.Shared;

internal static class EndOfDataSequence
{
    internal static void ProcessFromServer(BinaryWriter writer, BinaryReader reader)
    {
        byte[] outputBuffer = new byte[SequenceLength];
        byte[] inputBuffer = new byte[SequenceLength];

        RandomNumberGenerator generator = RandomNumberGenerator.Create();
        generator.GetNonZeroBytes(outputBuffer);

        writer.Write(outputBuffer);
        writer.Flush();

        int inputBufferIdx = 0;
        while (true)
        {
            ReadUntilFull(inputBuffer, reader);

            foreach (var b in outputBuffer)
            {
                if (inputBuffer[inputBufferIdx] == b)
                {
                    inputBufferIdx++;
                    if (inputBufferIdx == inputBuffer.Length)
                        return;

                    continue;
                }

                inputBufferIdx = 0;
            }
        }

    }

    internal static void ProcessFromClient(BinaryWriter writer, BinaryReader reader)
    {
        byte[] buffer = new byte[SequenceLength];
        ReadUntilFull(buffer, reader);
        writer.Write(buffer);
    }

    static void ReadUntilFull(byte[] dst, BinaryReader reader)
    {
        int read = 0;
        while (read < dst.Length)
            read += reader.Read(dst, read, dst.Length);
    }

    const int SequenceLength = 8 * sizeof(int);
}

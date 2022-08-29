using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace miloRPC.Core.Shared;

internal static class EndOfDataSequence
{
    internal static async Task ProcessFromServerAsync(
        BinaryWriter writer, BinaryReader reader, CancellationToken ct)
    {
        byte[] sendBuffer = new byte[SequenceLength];
        byte[] receiveBuffer = new byte[SequenceLength];

        RandomNumberGenerator generator = RandomNumberGenerator.Create();
        generator.GetNonZeroBytes(sendBuffer);

        writer.Write(sendBuffer);
        writer.Flush();

        Stream inputStream = reader.BaseStream;

        int sendBufferIdx = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            int read = await inputStream.ReadAsync(receiveBuffer, ct);

            for (int i = 0; i < read; i++)
            {
                if (sendBuffer[sendBufferIdx] == receiveBuffer[i])
                {
                    sendBufferIdx++;
                    if (sendBufferIdx == sendBuffer.Length)
                        return;

                    continue;
                }

                sendBufferIdx = 0;
            }
        }
    }

    internal static void ProcessFromClient(BinaryWriter writer, BinaryReader reader)
    {
        byte[] buffer = new byte[SequenceLength];
        ReadUntilFull(buffer, reader);
        writer.Write(buffer);
        writer.Flush();

        static void ReadUntilFull(byte[] dst, BinaryReader reader)
        {
            int read = 0;
            while (read < dst.Length)
                read += reader.Read(dst, read, dst.Length - read);
        }
    }

    const int SequenceLength = 8 * sizeof(int);
}

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using NUnit.Framework;

using miloRPC.Core.Shared;

namespace miloRPC.Tests;

[TestFixture]
public class RpcBrotliStreamTests
{
    [TestCase(1 * MiB, 5 * KiB, 1 * KiB, 3 * KiB)]
    [TestCase(1 * MiB, 1 * KiB, 3 * KiB, 5 * KiB)]
    public void Read_Should_Be_Equals_To_Write(
        int bufferSize,
        int bufferMaxChunkSize,
        int minChunkSize,
        int maxChunkSize)
    {
        ReadOnlySpan<char> textCorpusSource =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXY013456789".AsSpan();

        Random rand = new(Environment.TickCount);

        byte[] compressed = ArrayPool<byte>.Shared.Rent(
            RpcBrotliStream.GetMaxCompressedLength(bufferSize));
        byte[] source = ArrayPool<byte>.Shared.Rent(bufferSize);
        byte[] destination = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            for (int i = 0; i < bufferSize; i++)
                source[i] = (byte)textCorpusSource[rand.Next(0, textCorpusSource.Length)];

            MemoryStream ms = new(compressed);
            ThrottledStream ts = new(ms);

            RpcBrotliStream stream = new(ts, ArrayPool<byte>.Shared, bufferMaxChunkSize);

            int start = 0;
            while (start < bufferSize)
            {
                int nextChunkLength = Math.Min(
                    bufferSize - start,
                    rand.Next(minChunkSize, maxChunkSize));

                Span<byte> spanToWrite = new(source, start, nextChunkLength);

                stream.Write(spanToWrite);
                start += nextChunkLength;
            }

            Assert.That(
                ts.Position, Is.LessThan(bufferSize),
                "The RpcBrotliStream didn't compress a single byte!");

            ts.Position = 0;

            start = 0;
            while (start < bufferSize)
            {
                int nextChunkLength = Math.Min(
                    bufferSize - start,
                    rand.Next(minChunkSize, maxChunkSize));

                Span<byte> spanToRead = new(destination, start, nextChunkLength);

                stream.ReadUntilCountFulfilled(spanToRead);
                start += nextChunkLength;
            }

            for (int i = 0; i < bufferSize; i++)
            {
                Assert.That(
                    source[i], Is.EqualTo(destination[i]),
                    "Buffers differ at index {0}", i);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(compressed);
            ArrayPool<byte>.Shared.Return(source);
            ArrayPool<byte>.Shared.Return(destination);
        }
    }

    [TestCase(CompressionHeader.CompressionFlag.Compressed, 30)]
    [TestCase(CompressionHeader.CompressionFlag.Uncompressed, 30)]
    [TestCase(CompressionHeader.CompressionFlag.Compressed, 33)]
    [TestCase(CompressionHeader.CompressionFlag.Uncompressed, 33)]
    [TestCase(CompressionHeader.CompressionFlag.Compressed, 8190)]
    [TestCase(CompressionHeader.CompressionFlag.Uncompressed, 8190)]
    [TestCase(CompressionHeader.CompressionFlag.Compressed, 8194)]
    [TestCase(CompressionHeader.CompressionFlag.Uncompressed, 8194)]
    public void Encoded_Compression_Header_Should_Match_Decoded_Information(object flag, int size)
    {
        // Ugly workaround to use internal parameter type in public method
        // Suggested by Jon Skeet at:
        //     https://stackoverflow.com/questions/37379168/how-to-use-nunit-testcase-with-internal-type-as-argument#comment62269504_37379168
        CompressionHeader.CompressionFlag compressionFlag =
            (CompressionHeader.CompressionFlag) flag;
        
        byte[] buffer = new byte[5];
        MemoryStream ms = new(buffer);

        ms.Write(CompressionHeader.Create(compressionFlag, size));

        ms.Position = 0;

        byte[] readHeader = new byte[5];
        ms.Read(readHeader, 0, 1);

        CompressionHeader.Decode(
            readHeader,
            out CompressionHeader.CompressionFlag decodedCompressionFlag,
            out CompressionHeader.SizeFlag decodedSizeFlag);

        Assert.That(decodedCompressionFlag, Is.EqualTo(compressionFlag));

        if (decodedSizeFlag == CompressionHeader.SizeFlag.Small)
            ms.Read(readHeader, 1, 1);

        if (decodedSizeFlag == CompressionHeader.SizeFlag.Regular)
            ms.Read(readHeader, 1, 4);

        int decodedSize = CompressionHeader.GetSize(readHeader, decodedSizeFlag);

        Assert.That(decodedSize, Is.EqualTo(size));
    }

    const int KiB = 1024;
    const int MiB = 1024 * 1024;
}

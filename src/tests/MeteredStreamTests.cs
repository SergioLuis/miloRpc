using System;
using System.IO;
using System.Threading.Tasks;

using NUnit.Framework;

using dotnetRpc.Core.Channels;

namespace dotnetRpc.Tests;

[TestFixture]
public class MeteredStreamTests
{
    [Test]
    public void Write_Is_Correct_And_Increases_WrittenBytes()
    {
        Random rand = new(Environment.TickCount);

        int limit = 1024 * 1024;

        int written = 0;
        byte[] inBuffer = new byte[50 * 1024];
        byte[] outBuffer = new byte[50 * 1024];

        MemoryStream ms = new(inBuffer);
        MeteredStream meteredStream = new(ms);

        Assert.That(meteredStream.WriteTime, Is.EqualTo(TimeSpan.Zero));

        while (written <= limit)
        {
            ms.Position = 0;
            rand.NextBytes(outBuffer);

            int nextChunkLen = rand.Next(20, 50) * 1024;

            meteredStream.Write(outBuffer, 0, nextChunkLen);

            written += nextChunkLen;

            Assert.That(written, Is.EqualTo(meteredStream.WrittenBytes));

            for (int i = 0; i < nextChunkLen; i++)
                Assert.That(inBuffer[i], Is.EqualTo(outBuffer[i]));
        }

        Assert.That(meteredStream.WriteTime, Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public async Task WriteAsync_Is_Correct_And_Increases_WrittenBytes()
    {
        Random rand = new(Environment.TickCount);

        int limit = 1024 * 1024;

        int written = 0;
        byte[] inBuffer = new byte[50 * 1024];
        byte[] outBuffer = new byte[50 * 1024];

        MemoryStream ms = new(inBuffer);
        MeteredStream meteredStream = new(ms);

        Assert.That(meteredStream.WriteTime, Is.EqualTo(TimeSpan.Zero));

        while (written <= limit)
        {
            ms.Position = 0;
            rand.NextBytes(outBuffer);

            int nextChunkLen = rand.Next(20, 50) * 1024;

            await meteredStream.WriteAsync(outBuffer, 0, nextChunkLen);

            written += nextChunkLen;

            Assert.That(written, Is.EqualTo(meteredStream.WrittenBytes));

            Console.WriteLine($"{written}/{limit}");

            for (int i = 0; i < nextChunkLen; i++)
                Assert.That(inBuffer[i], Is.EqualTo(outBuffer[i]));
        }

        Assert.That(meteredStream.WriteTime, Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public void Read_Is_Correct_And_Increases_ReadBytes()
    {
        Random rand = new(Environment.TickCount);

        int limit = 1024 * 1024;

        int read = 0;
        byte[] inBuffer = new byte[50 * 1024];
        byte[] outBuffer = new byte[50 * 1024];

        MemoryStream ms = new(outBuffer);
        MeteredStream meteredStream = new(ms);

        Assert.That(meteredStream.ReadTime, Is.EqualTo(TimeSpan.Zero));

        while (read <= limit)
        {
            ms.Position = 0;
            rand.NextBytes(outBuffer);

            int nextChunkLen = rand.Next(20, 50) * 1024;

            int r = meteredStream.Read(inBuffer, 0, nextChunkLen);
            Assert.That(r, Is.EqualTo(nextChunkLen));

            read += r;

            Assert.That(read, Is.EqualTo(meteredStream.ReadBytes));

            for (int i = 0; i < nextChunkLen; i++)
                Assert.That(inBuffer[i], Is.EqualTo(outBuffer[i]));
        }

        Assert.That(meteredStream.ReadTime, Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public async Task ReadAsync_Is_Correct_And_Increases_ReadBytes()
    {
        Random rand = new(Environment.TickCount);

        int limit = 1024 * 1024;

        int read = 0;
        byte[] inBuffer = new byte[50 * 1024];
        byte[] outBuffer = new byte[50 * 1024];

        MemoryStream ms = new(outBuffer);
        MeteredStream meteredStream = new(ms);

        Assert.That(meteredStream.ReadTime, Is.EqualTo(TimeSpan.Zero));

        while (read <= limit)
        {
            ms.Position = 0;
            rand.NextBytes(outBuffer);

            int nextChunkLen = rand.Next(20, 50) * 1024;

            int r = await meteredStream.ReadAsync(inBuffer, 0, nextChunkLen);
            Assert.That(r, Is.EqualTo(nextChunkLen));

            read += r;

            Assert.That(read, Is.EqualTo(meteredStream.ReadBytes));

            for (int i = 0; i < nextChunkLen; i++)
                Assert.That(inBuffer[i], Is.EqualTo(outBuffer[i]));
        }

        Assert.That(meteredStream.ReadTime, Is.GreaterThan(TimeSpan.Zero));
    }
}

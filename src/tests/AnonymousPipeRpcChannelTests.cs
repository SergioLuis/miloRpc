using System;
using System.IO;
using System.Threading.Tasks;

using NUnit.Framework;

using dotnetRpc.Core.Channels;

namespace dotnetRpc.Tests;

[TestFixture]
public class AnonymousPipeRpcChannelTests
{
    [TestCase(1)]
    [TestCase(10)]
    [TestCase(100)]
    public async Task Connection_Completes_And_Can_Be_Used(int conns)
    {
        DirectoryInfo directory = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        Console.WriteLine(directory.FullName);
        try
        {
            AnonymousPipeListener.PoolSettings poolSettings = new()
            {
                LowerLimit = 5,
                GrowthRate = 10
            };

            AnonymousPipeListener listener = new(
                directory.FullName,
                string.Empty,
                poolSettings);

            AnonymousPipeClient client = new(
                directory.FullName,
                string.Empty,
                5);

            await listener.Start();
            await client.Start();

            Func<Task> serverAction = async () =>
            {
                for (int i = 0; i < conns; i++)
                {
                    IRpcChannel channel = await listener.AcceptPipeAsync();

                    BinaryWriter writer = new(channel.Stream);
                    BinaryReader reader = new(channel.Stream);

                    writer.Write(reader.ReadInt32());

                    channel.Dispose();
                }
            };

            Func<Task> clientAction = async () =>
            {
                Random r = new Random(Environment.TickCount);

                for (int i = 0; i < conns; i++)
                {
                    IRpcChannel channel = await client.ConnectAsync();

                    BinaryWriter writer = new(channel.Stream);
                    BinaryReader reader = new(channel.Stream);

                    int nextInt = r.Next();
                    writer.Write(nextInt);

                    Assert.That(reader.ReadInt32(), Is.EqualTo(nextInt));

                    channel.Dispose();
                }
            };

            Task serverTask = Task.Run(serverAction);
            Task clientTask = Task.Run(clientAction);

            await Task.WhenAll(serverTask, clientTask);

            listener.Dispose();
            client.Dispose();
        }
        finally
        {
            directory.Delete(true);
        }
    }
}

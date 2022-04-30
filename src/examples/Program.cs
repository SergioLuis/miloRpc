using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Extensions.Logging;

using dotnetRpc.Core.Channels;
using dotnetRpc.Core.Shared;

namespace dotnetRpc.Examples;

static class Program
{
    static async Task<int> Main(string[] args)
    {
        ConfigureLogging();

        string directory = Path.GetTempPath();
        string prefix = string.Concat(Guid.NewGuid().ToString()[..8], '_');
        CancellationTokenSource cts = new();

        Task serverTask = RunServer.Run(directory, prefix, cts.Token);
        await Task.Delay(3 * 1000, cts.Token); // Wait for the server to listen to requests

        Task client1Task = RunClient.Run(directory, prefix, cts.Token);
        Task client2Task = RunClient.Run(directory, prefix, cts.Token);
        Task client3Task = RunClient.Run(directory, prefix, cts.Token);

        await Task.WhenAll(client1Task, client2Task, client3Task);

        cts.Cancel();
        await serverTask;

        return 0;
    }

    static void ConfigureLogging()
    {
        var config = new LoggingConfiguration();

        var consoleTarget = new ColoredConsoleTarget();
        consoleTarget.Layout = NLOG_LAYOUT;

        var fileTarget = new FileTarget();
        fileTarget.Layout = NLOG_LAYOUT;
        fileTarget.FileName = NLOG_FILE;
        fileTarget.KeepFileOpen = true;

        var consoleLoggingRule =
            new LoggingRule("*", LogLevel.Trace, consoleTarget);
        var fileLoggingRule =
            new LoggingRule("*", LogLevel.Trace, fileTarget);

        config.AddTarget("console", consoleTarget);
        config.AddTarget("file", fileTarget);

        config.LoggingRules.Add(consoleLoggingRule);
        config.LoggingRules.Add(fileLoggingRule);

        LogManager.Configuration = config;
        RpcLoggerFactory.RegisterLoggerFactory(new NLogLoggerFactory());
    }

    const string NLOG_FILE = "${basedir}/example.log.txt";
    const string NLOG_LAYOUT = @"${date:format=HH\:mm\:ss.fff} ${logger} - ${message}";

    static class RunClient
    {
        public static async Task<bool> Run(
            string directory, string prefix, CancellationToken ct)
        {
            Random random = new Random(Environment.TickCount);

            AnonymousPipeClient client = new(directory, prefix);
            await client.Start();
            try
            {
                while (true)
                {
                    IRpcChannel channel = await client.ConnectAsync(ct);
                    try
                    {
                        BinaryReader reader = new(channel.Stream);
                        BinaryWriter writer = new(channel.Stream);

                        int toWrite = random.Next();
                        writer.Write(toWrite);

                        int read = reader.ReadInt32();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                    }
                    finally
                    {
                        channel.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return false;
            }
        }
    }

    static class RunServer
    {
        public static async Task<bool> Run(
            string directory, string prefix, CancellationToken ct)
        {
            AnonymousPipeListener.PoolSettings poolSettings = new()
            {
                LowerLimit = 10,
                GrowthRate = 10
            };

            AnonymousPipeListener listener = new(directory, prefix, poolSettings);
            await listener.Start();
            try
            {
                while (true)
                {
                    IRpcChannel channel = await listener.AcceptPipeAsync(ct);
                    try
                    {
                        BinaryReader reader = new(channel.Stream);
                        BinaryWriter writer = new(channel.Stream);

                        int read = reader.ReadInt32();
                        writer.Write(read);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                    }
                    finally
                    {
                        channel.Dispose();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return false;
            }
        }
    }
}

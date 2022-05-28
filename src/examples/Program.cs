using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Extensions.Logging;

using miloRPC.Channels.Tcp;
using miloRPC.Core.Client;
using miloRPC.Core.Server;
using miloRPC.Core.Shared;
using miloRPC.Examples.Client;
using miloRPC.Examples.Server;
using miloRPC.Examples.Shared;

namespace miloRPC.Examples;

static class Program
{
    static async Task<int> Main(string[] args)
    {
        ConfigureLogging();
        ExampleSerializers.RegisterSerializers();

        CancellationTokenSource cts = new();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.WriteLine("Press CTRL + C to exit");
        await Task.Delay(3000, cts.Token);

        IPEndPoint ipEndPoint = new(IPAddress.Loopback, 9876);

        Task serverTask = RunServer.Run(ipEndPoint, cts.Token);
        await Task.Delay(3 * 1000, cts.Token); // Wait for the server to listen to requests

        ConnectionPool pool = new(new ConnectToTcpServer(ipEndPoint));

        Task client1Task = RunClient.Run(pool, cts.Token);
        Task client2Task = RunClient.Run(pool, cts.Token);
        Task client3Task = RunClient.Run(pool, cts.Token);

        await Task.WhenAll(client1Task, client2Task, client3Task);

        await serverTask;

        return 0;
    }

    static void ConfigureLogging()
    {
        var config = new LoggingConfiguration();

        var consoleTarget = new ColoredConsoleTarget();
        consoleTarget.Layout = NLogLayout;

        var fileTarget = new FileTarget();
        fileTarget.Layout = NLogLayout;
        fileTarget.FileName = NLogFile;
        fileTarget.KeepFileOpen = true;

        LoggingRule consoleLoggingRule = new("*", LogLevel.Trace, consoleTarget);
        LoggingRule fileLoggingRule = new("*", LogLevel.Trace, fileTarget);

        config.AddTarget("console", consoleTarget);
        config.AddTarget("file", fileTarget);

        config.LoggingRules.Add(consoleLoggingRule);
        config.LoggingRules.Add(fileLoggingRule);

        LogManager.Configuration = config;
        RpcLoggerFactory.RegisterLoggerFactory(new NLogLoggerFactory());
    }

    const string NLogFile = "${basedir}/example.log.txt";
    const string NLogLayout = @"${date:format=HH\:mm\:ss.fff} ${logger} - ${message}";

    static class RunClient
    {
        public static async Task<bool> Run(ConnectionPool connPool, CancellationToken ct)
        {
            try
            {
                IEchoService echoService = new EchoServiceProxy(connPool);
                while (!ct.IsCancellationRequested)
                {
                    string reqValue = Guid.NewGuid().ToString();
                    DateTime reqDate = DateTime.UtcNow;

                    EchoResult result = await echoService.EchoAsync(reqValue, ct);

                    Console.WriteLine(
                        $"{result.ReceptionDateUtc - reqDate}: {reqValue == result.ReceivedMessage}");

                    await Task.Delay(100);
                }

                return true;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    return true;

                await Console.Error.WriteLineAsync(ex.Message);
                await Console.Error.WriteLineAsync(ex.StackTrace);
                return false;
            }
        }
    }

    static class RunServer
    {
        public static async Task<bool> Run(IPEndPoint bindEndpoint, CancellationToken ct)
        {
            StubCollection stubs = new(new EchoServiceStub(new EchoService()));
            IServer server = new TcpServer(bindEndpoint, stubs);

            Task serverTask = server.ListenAsync(ct);
            try
            {
                await serverTask;
                return true;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync(ex.Message);
                await Console.Error.WriteLineAsync(ex.StackTrace);
                return false;
            }
        }
    }
}

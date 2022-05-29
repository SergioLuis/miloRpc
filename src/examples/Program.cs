using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Extensions.Logging;

using miloRPC.Channels.Quic;
using miloRPC.Core.Client;
using miloRPC.Core.Server;
using miloRPC.Core.Shared;
using miloRPC.Examples.Client;
using miloRPC.Examples.Server;
using miloRPC.Examples.Shared;

namespace miloRPC.Examples;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("macOS")]
static class Program
{
    static async Task<int> Main(string[] args)
    {
        ConfigureLogging();
        ExampleSerializers.RegisterSerializers();

        const int numClients = 3;
        const string applicationProtocol = "miloRPC-demo";

        string certificatePath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            "demo.pfx");

        INegotiateServerQuicRpcProtocol negotiateServerProtocol =
            new DefaultQuicServerProtocolNegotiation(
                RpcCapabilities.None,
                RpcCapabilities.None,
                ArrayPool<byte>.Shared,
                new []{ new SslApplicationProtocol(applicationProtocol)},
                certificatePath,
                "th1s_c3rt_1s_s3lf_s1gn3d");

        INegotiateClientQuicRpcProtocol negotiateClientProtocol =
            new DefaultQuicClientProtocolNegotiation(
                RpcCapabilities.None,
                RpcCapabilities.None,
                ArrayPool<byte>.Shared,
                new[] {new SslApplicationProtocol(applicationProtocol)},
                DefaultQuicClientProtocolNegotiation.AcceptAllCertificates);

        CancellationTokenSource cts = new();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        IPEndPoint ipEndPoint = new(IPAddress.Loopback, 9876);

        Task serverTask = await RunServer.StartServer(ipEndPoint, negotiateServerProtocol, cts.Token);

        ConnectionPool pool = new(new ConnectToQuicServer(ipEndPoint, negotiateClientProtocol));

        List<Task> tasksToWaitFor = new(numClients + 1);
        for (int i = 1; i < numClients + 1; i++)
            tasksToWaitFor.Add(RunClient.Run(pool, cts.Token));

        tasksToWaitFor.Add(serverTask);

        await Task.WhenAll(tasksToWaitFor);

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
        public static async Task<Task> StartServer(
            IPEndPoint bindEndpoint,
            INegotiateServerQuicRpcProtocol negotiateRpcProtocol,
            CancellationToken ct)
        {
            StubCollection stubs = new(new EchoServiceStub(new EchoService()));
            IServer<IPEndPoint> server = new QuicServer(bindEndpoint, stubs, negotiateRpcProtocol);

            TaskCompletionSource startServerTcs = new TaskCompletionSource();

            server.AcceptLoopStart += (_, _) => startServerTcs.SetResult();

            Task serverTask = server.ListenAsync(ct);

            // By the time this function returns, the server is ready to accept connections
            await startServerTcs.Task;

            Console.WriteLine($"Server based on {server.ServerProtocol} ready to accept connections");

            return serverTask;
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;

using dotnetRpc.Client;
using dotnetRpc.Server;

namespace Orchestrator.Rpc;

class Program
{
    static async Task<int> Main(string[] args)
    {
        CancellationTokenSource cts = new();
        cts.CancelAfter(30 * 1000);

        Task serverTask = RunServer.Run(cts.Token);
        await Task.Delay(3 * 1000); // Wait for the server to listen to requests

        Task clientTask = RunClient.Run(cts.Token);

        await Task.WhenAll(serverTask, clientTask);

        return 0;
    }

    static class RunClient
    {
        public static async Task<bool> Run(CancellationToken ct)
        {
            try
            {
                ConnectToServer connectToServer = new(endpoint);
                ConnectionToServer connectionToServer =
                    await connectToServer.ConnectAsync(30000, ct);

                Console.WriteLine("Connection stablished!");

                await Task.Delay(10_000);
                connectionToServer.InvokeEchoRequest("Echo 1");
                connectionToServer.InvokeEchoRequest("Echo 2");
                connectionToServer.InvokeEchoRequest("Echo 3");
                connectionToServer.InvokeEchoRequest("Echo 4");
                connectionToServer.InvokeEchoRequest("Echo 5");

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

    static class RunServer
    {
        public static async Task<bool> Run(CancellationToken ct)
        {
            try
            {
                IServer tcpServer = new TcpServer(endpoint);
                await tcpServer.ListenAsync(ct);
                Console.WriteLine("RunServer ListenAsync task completed without errors");
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

    static readonly IPEndPoint endpoint = new(IPAddress.Loopback, 7890);
}

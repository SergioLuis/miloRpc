using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;

using dotnetRpc.Client;
using dotnetRpc.Server;
using dotnetRpc.Shared;

namespace Orchestrator.Rpc;

class Program
{
    static async Task<int> Main(string[] args)
    {
        CancellationTokenSource cts = new();
        cts.CancelAfter(30 * 1000);

        Task serverTask = RunServer.Run(cts.Token);
        await Task.Delay(3 * 1000); // Wait for the server to listen to requests

        Task client1Task = RunClient.Run("client 1", cts.Token);
        Task client2Task = RunClient.Run("client 2", cts.Token);
        Task client3Task = RunClient.Run("client 3", cts.Token);

        await Task.WhenAll(serverTask, client1Task, client2Task, client3Task);

        return 0;
    }

    static class RunClient
    {
        public static async Task<bool> Run(string clientName, CancellationToken ct)
        {
            try
            {
                DefaultClientProtocolNegotiation protocolNegotiation = new(
                    RpcCapabilities.None,
                    RpcCapabilities.None,
                    Compression.None);
                ConnectToServer connectToServer = new(endpoint, protocolNegotiation);
                ConnectionToServer connectionToServer =
                    await connectToServer.ConnectAsync(30000, ct);

                Console.WriteLine("Connection stablished!");

                await Task.Delay(10_000);
                Console.WriteLine(connectionToServer.InvokeEchoRequest($"{clientName} Echo 1"));
                Console.WriteLine(connectionToServer.InvokeEchoRequest($"{clientName} Echo 2"));
                Console.WriteLine(connectionToServer.InvokeEchoRequest($"{clientName} Echo 3"));
                Console.WriteLine(connectionToServer.InvokeEchoRequest($"{clientName} Echo 4"));
                Console.WriteLine(connectionToServer.InvokeEchoRequest($"{clientName} Echo 5"));

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
                DefaultServerProtocolNegotiation protocolNegotiation = new(
                    RpcCapabilities.None,
                    RpcCapabilities.None,
                    Compression.None);
                IServer tcpServer = new TcpServer(endpoint, protocolNegotiation, Timeout.Infinite);
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

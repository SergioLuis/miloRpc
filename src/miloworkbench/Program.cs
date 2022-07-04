using System;
using System.Threading;
using System.Threading.Tasks;

using miloRpc.WorkBench.Client;
using miloRpc.WorkBench.Server;

namespace miloRpc.TestWorkBench.Server;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("miloworkbench [client|server] [args]");
            return 1;
        }

        Func<string[], CancellationToken, Task<int>> funcToRun = args[0].ToLowerInvariant() switch
        {
            "server" => RunServer.RunAsync,
            "client" => RunClient.RunAsync,
            _ => throw new NotSupportedException($"Invalid argument '{args[0]}'")
        };

        return await funcToRun(args[1..], CancellationToken.None);
    }
}

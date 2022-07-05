using System.Threading.Tasks;

using Spectre.Console.Cli;

using miloRpc.TestWorkBench.Server.Commands;

namespace miloRpc.TestWorkBench.Server;

public static class RunServer
{
    public static async Task<int> RunAsync(string[] args)
    {
        var commandApp = new CommandApp();
        commandApp.Configure(config =>
        {
            config.AddCommand<ListenCommand>("listen")
                .WithDescription("Starts the requested miloRpc servers");
        });

        return await commandApp.RunAsync(args);
    }
}

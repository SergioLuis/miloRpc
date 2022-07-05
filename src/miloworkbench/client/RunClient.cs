using System.Threading.Tasks;

using Spectre.Console.Cli;

using miloRpc.TestWorkBench.Client.Commands;

namespace miloRpc.TestWorkBench.Client;

public static class RunClient
{
    public static async Task<int> RunAsync(string[] args)
    {
        var commandApp = new CommandApp();
        commandApp.Configure(config =>
        {
            config.AddCommand<SpeedTestCommand>("speedtest")
                .WithDescription("Runs a speed test against the specified server");
        });

        return await commandApp.RunAsync(args);
    }
}

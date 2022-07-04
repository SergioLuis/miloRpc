using System.Threading;
using System.Threading.Tasks;

namespace miloRpc.WorkBench.Server;

public static class RunServer
{
    public static async Task<int> RunAsync(string[] args, CancellationToken ct = default)
    {
        await Task.Delay(1000, ct);
        return 0;
    }
}

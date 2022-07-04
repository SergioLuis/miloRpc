using System.Threading;
using System.Threading.Tasks;

namespace miloRpc.WorkBench.Client;

public static class RunClient
{
    public static async Task<int> RunAsync(string[] args, CancellationToken ct = default)
    {
        await Task.Delay(1000, ct);
        return 0;
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

using miloRPC.Examples.Shared;

namespace miloRPC.Examples.Server;

public class EchoService : IEchoService
{
    public Task<EchoResult> EchoAsync(string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new EchoResult(DateTime.UtcNow, message));
    }
}

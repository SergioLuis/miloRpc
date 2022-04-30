using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace miloRPC.Core.Channels;

public interface IRpcChannel : IDisposable
{
    MeteredStream Stream { get; }
    IPEndPoint RemoteEndPoint { get; }

    ValueTask WaitForDataAsync(CancellationToken ct);
    bool IsConnected();
}

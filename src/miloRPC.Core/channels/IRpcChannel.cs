using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace miloRPC.Core.Channels;

public interface IRpcChannel : IDisposable
{
    string ChannelProtocol { get; }
    MeteredStream Stream { get; }
    IPEndPoint LocalEndPoint { get; }
    IPEndPoint RemoteEndPoint { get; }

    ValueTask WaitForDataAsync(CancellationToken ct);
    bool IsConnected();
}

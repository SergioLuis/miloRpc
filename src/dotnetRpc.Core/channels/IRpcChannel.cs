using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Channels;

internal interface IRpcChannel : IDisposable
{
    MeteredStream Stream { get; }
    IPEndPoint RemoteEndPoint { get; }

    ValueTask WaitForDataAsync(CancellationToken ct);
    bool IsConnected();
}

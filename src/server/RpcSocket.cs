using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using dotnetRpc.Shared;

namespace dotnetRpc.Server;

internal class RpcSocket
{
    internal MeteredStream Stream => mMeteredStream;

    internal RpcSocket(Socket socket)
    {
        mSocket = socket;
        mMeteredStream = new(new NetworkStream(mSocket));
    }

    internal async ValueTask BeginReceiveAsync(CancellationToken ct)
    {
        await mSocket.ReceiveAsync(Memory<byte>.Empty, SocketFlags.None, ct);
    }

    readonly Socket mSocket;
    readonly MeteredStream mMeteredStream;
}

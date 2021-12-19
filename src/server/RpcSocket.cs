using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using dotnetRpc.Shared;

namespace dotnetRpc.Server;

internal class RpcSocket
{
    internal MeteredStream Stream => mMeteredStream;
    internal IPEndPoint RemoteEndPoint => mRemoteEndPoint;

    internal RpcSocket(Socket socket, CancellationToken ct)
    {
        mSocket = socket;
        mMeteredStream = new(new NetworkStream(mSocket));
        mRemoteEndPoint = (IPEndPoint)mSocket.RemoteEndPoint!;

        ct.Register(CloseSocket, socket);
    }

    internal async ValueTask BeginReceiveAsync(CancellationToken ct)
    {
        await mSocket.ReceiveAsync(Memory<byte>.Empty, SocketFlags.None, ct);
    }

    static void CloseSocket(object? state)
    {
        Socket? socket = state as Socket;
        if (socket == null)
            return;

        socket.Shutdown(SocketShutdown.Both);
        socket.Close();
    }

    readonly Socket mSocket;
    readonly MeteredStream mMeteredStream;
    readonly IPEndPoint mRemoteEndPoint;
}

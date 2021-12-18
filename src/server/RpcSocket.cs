using System.IO;
using System.Net.Sockets;

using dotnetRpc.Shared;

namespace dotnetRpc.Server;

internal class RpcSocket
{
    internal RpcSocket(Socket socket)
    {
        mSocket = socket;
    }

    public Stream GetNetworkStream()
        => new MeteredStream(new NetworkStream(mSocket));

    readonly Socket mSocket;
}

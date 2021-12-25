using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
        mLog = RpcLoggerFactory.CreateLogger("RpcSocket");

        ct.Register(() =>
        {
            mLog.LogTrace("Cancellation requested, closing RpcSocket");
            Close();
            mLog.LogTrace("RpcSocket closed");
        });
    }

    internal async ValueTask WaitForDataAsync(CancellationToken ct)
    {
        // The call returns once there is new data to be read in the socket,
        // without actually reading anything.
        await mSocket.ReceiveAsync(Memory<byte>.Empty, SocketFlags.None, ct);
    }

    internal bool IsConnected()
    {
        // TODO: Missing implementation
        return true;
    }

    internal void Close()
    {
        lock (this)
        {
            if (mbIsClosed)
                return;

            try
            {
                mSocket.Shutdown(SocketShutdown.Both);
                mSocket.Close();
            }
            catch (Exception ex)
            {
                mLog.LogError("There was an error closing RpcSocket: {0}", ex.Message);
                mLog.LogDebug("StackTrace:{0}{1}", Environment.NewLine, ex.StackTrace);
            }
            finally
            {
                mbIsClosed = true;
            }
        }
    }

    bool mbIsClosed = false;
    readonly Socket mSocket;
    readonly MeteredStream mMeteredStream;
    readonly IPEndPoint mRemoteEndPoint;
    readonly ILogger mLog;
}

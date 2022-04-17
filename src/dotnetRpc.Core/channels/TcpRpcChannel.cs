using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Channels;

internal class TcpRpcChannel : IRpcChannel
{
    MeteredStream IRpcChannel.Stream => mMeteredStream;
    IPEndPoint IRpcChannel.RemoteEndPoint => mRemoteEndPoint;

    internal TcpRpcChannel(Socket socket, CancellationToken ct)
    {
        mSocket = socket;
        mMeteredStream = new MeteredStream(new NetworkStream(mSocket));
        mRemoteEndPoint = (IPEndPoint)mSocket.RemoteEndPoint!;
        mLog = RpcLoggerFactory.CreateLogger("RpcSocket");

        ct.Register(() =>
        {
            mLog.LogTrace("Cancellation requested, closing RpcSocket");
            Dispose();
            mLog.LogTrace("RpcSocket closed");
        });
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    async ValueTask IRpcChannel.WaitForDataAsync(CancellationToken ct)
        => await mSocket.ReceiveAsync(Memory<byte>.Empty, SocketFlags.None, ct);

    bool IRpcChannel.IsConnected()
    {
        if (mDisposed)
            return false;

        bool pollVal = mSocket.Poll(1000, SelectMode.SelectRead);
        bool availableVal = mSocket.Available == 0;
        return !(pollVal && availableVal);
    }

    void Close()
    {
        lock (this)
        {
            if (mDisposed)
                return;

            try
            {
                mSocket.Shutdown(SocketShutdown.Both);
                mSocket.Close();
            }
            catch (Exception ex)
            {
                mLog.LogError("There was an error closing RpcSocket: {ExMessage}", ex.Message);
                mLog.LogDebug("StackTrace: {ExStackTrace}", ex.StackTrace);
            }
            finally
            {
                mDisposed = true;
            }
        }
    }

    volatile bool mDisposed = false;
    readonly Socket mSocket;
    readonly MeteredStream mMeteredStream;
    readonly IPEndPoint mRemoteEndPoint;
    readonly ILogger mLog;
}

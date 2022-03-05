using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace dotnetRpc.Core.Shared;

internal class RpcSocket : IDisposable
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
            Dispose();
            mLog.LogTrace("RpcSocket closed");
        });
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    internal async ValueTask WaitForDataAsync(CancellationToken ct)
        => await mSocket.ReceiveAsync(Memory<byte>.Empty, SocketFlags.None, ct);

    internal bool IsConnected()
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
                mLog.LogError("There was an error closing RpcSocket: {0}", ex.Message);
                mLog.LogDebug("StackTrace:{0}{1}", Environment.NewLine, ex.StackTrace);
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

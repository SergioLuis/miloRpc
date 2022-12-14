using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using miloRPC.Core.Channels;
using miloRPC.Core.Shared;

namespace miloRPC.Channels.Tcp;

internal class TcpRpcChannel : IRpcChannel
{
    string IRpcChannel.ChannelProtocol => WellKnownProtocols.TCP;
    MeteredStream IRpcChannel.Stream => mMeteredStream;
    IPEndPoint IRpcChannel.LocalEndPoint => mLocalEndPoint;
    IPEndPoint IRpcChannel.RemoteEndPoint => mRemoteEndPoint;

    internal TcpRpcChannel(Socket socket, CancellationToken ct)
    {
        mSocket = socket;
        mMeteredStream = new MeteredStream(new NetworkStream(mSocket));
        mLocalEndPoint = Unsafe.As<IPEndPoint>(mSocket.LocalEndPoint!);
        mRemoteEndPoint = Unsafe.As<IPEndPoint>(mSocket.RemoteEndPoint!);
        mLog = RpcLoggerFactory.CreateLogger("TcpRpcChannel");

        ct.Register(() =>
        {
            mLog.LogTrace("Cancellation requested, closing TcpRpcChannel");
            Dispose();
            mLog.LogTrace("TcpRpcChannel closed");
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
        lock (mCloseSyncLock)
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
                mLog.LogError("There was an error closing TcpRpcChannel: {ExMessage}", ex.Message);
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
    readonly IPEndPoint mLocalEndPoint;
    readonly IPEndPoint mRemoteEndPoint;
    readonly ILogger mLog;

    readonly object mCloseSyncLock = new();
}

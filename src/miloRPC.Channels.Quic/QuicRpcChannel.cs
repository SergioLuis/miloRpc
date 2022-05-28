using System;
using System.Net;
using System.Net.Quic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using miloRPC.Core.Channels;
using miloRPC.Core.Shared;

namespace miloRPC.Channels.Quic;

[SupportedOSPlatform("linux")]
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("macOS")]
internal class QuicRpcChannel : IRpcChannel
{
    MeteredStream IRpcChannel.Stream => mMeteredStream;
    IPEndPoint IRpcChannel.RemoteEndPoint => mRemoteEndpoint;

    internal QuicRpcChannel(QuicConnection conn, CancellationToken ct)
    {
        mConnection = conn;
        mMeteredStream = new MeteredStream(mConnection.OpenBidirectionalStream());
        mRemoteEndpoint = (IPEndPoint)conn.RemoteEndPoint;
        mLog = RpcLoggerFactory.CreateLogger("QuicRpcChannel");

        ct.Register(() =>
        {
            mLog.LogTrace("Cancellation requested, closing QuicRpcChannel");
            Dispose();
            mLog.LogTrace("QuicRpcChannel closed");
        });
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    public async ValueTask WaitForDataAsync(CancellationToken ct)
        => await mMeteredStream.ReadAsync(Memory<byte>.Empty, ct);

    public bool IsConnected() => !mDisposed && mConnection.Connected;

    void Close()
    {
        lock (mCloseSyncLock)
        {
            if (mDisposed)
                return;

            try
            {
                mConnection.CloseAsync(0x00, CancellationToken.None).AsTask().Wait();
                mConnection.Dispose();
            }
            catch (Exception ex)
            {
                mLog.LogError("There was an error closing QuicRpcChannel: {ExMessage}", ex.Message);
                mLog.LogDebug("StackTrace: {ExStackTrace}", ex.StackTrace);
            }
            finally
            {
                mDisposed = true;
            }
        }
    }

    volatile bool mDisposed = false;
    readonly QuicConnection mConnection;
    readonly MeteredStream mMeteredStream;
    readonly IPEndPoint mRemoteEndpoint;
    readonly ILogger mLog;

    readonly object mCloseSyncLock = new();
}

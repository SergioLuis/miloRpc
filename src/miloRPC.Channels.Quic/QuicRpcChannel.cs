using System;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Quic;
using System.Runtime.CompilerServices;
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
    enum ConnectionSide
    {
        Client,
        Server
    }

    string IRpcChannel.ChannelProtocol => WellKnownProtocols.QUIC;
    MeteredStream IRpcChannel.Stream => mMeteredStream ?? MeteredStream.MeteredNull;
    IPEndPoint IRpcChannel.LocalEndPoint => Unsafe.As<IPEndPoint>(mConnection.LocalEndPoint!);
    IPEndPoint IRpcChannel.RemoteEndPoint => Unsafe.As<IPEndPoint>(mConnection.RemoteEndPoint);

    QuicRpcChannel(
        ConnectionSide side,
        QuicConnection conn,
        MeteredStream? meteredStream,
        CancellationToken ct)
    {
        mSide = side;
        mConnection = conn;
        mMeteredStream = meteredStream;
        mLog = RpcLoggerFactory.CreateLogger("QuicRpcChannel");

        ct.Register(() =>
        {
            mLog.LogTrace("Cancellation requested, closing QuicRpcChannel");
            Dispose();
            mLog.LogTrace("QuicRpcChannel closed");
        });
    }

    internal static IRpcChannel CreateForServer(QuicConnection conn, CancellationToken ct)
        => new QuicRpcChannel(
            ConnectionSide.Server,
            conn,
            null,
            ct);

    internal static IRpcChannel CreateForClient(QuicConnection conn, CancellationToken ct)
        => new QuicRpcChannel(
            ConnectionSide.Client,
            conn,
            new MeteredStream(conn.OpenBidirectionalStream()),
            ct);

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    public async ValueTask WaitForDataAsync(CancellationToken ct)
    {
        if (mSide == ConnectionSide.Server && mMeteredStream is null)
        {
            // AcceptStreamAsync blocks until the other side calls 'OpenBidirectionalStream'
            // and (for some reason) actually writes some data.
            //
            // Calling this method from QuicServer's AcceptLoop would cause a deadlock
            // if the client opens a connection that's not going to be used immediately
            // (which happens when pooling connections)
            mMeteredStream = new MeteredStream(await mConnection.AcceptStreamAsync(ct));
        }

        Contract.Assert(mMeteredStream is not null);
        await mMeteredStream.ReadAsync(Memory<byte>.Empty, ct);
    }

    public bool IsConnected() => !mDisposed && mConnection.Connected;

    void Close()
    {
        lock (mCloseSyncLock)
        {
            if (mDisposed)
                return;

            try
            {
                mMeteredStream?.GetInnerStream<QuicStream>().Shutdown();
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
    MeteredStream? mMeteredStream;

    readonly ConnectionSide mSide;
    readonly QuicConnection mConnection;
    readonly ILogger mLog;

    readonly object mCloseSyncLock = new();
}

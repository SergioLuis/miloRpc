using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Client;

public class ConnectionToServer : IDisposable
{
    public enum Status
    {
        Idling,
        NegotiatingProtocol,
        Writing,
        Waiting,
        Reading,
        Exited
    }

    public uint ConnectionId => mConnectionId;
    public IPEndPoint RemoteEndPoint => IPEndPoint.Parse("127.0.0.1:9876");

    public TimeSpan CurrentIdlingTime => TimeSpan.Zero; // FIXME: Implement
    public TimeSpan CurrentWritingTime => TimeSpan.Zero; // FIXME: Implement
    public TimeSpan CurrentWaitingTime => TimeSpan.Zero; // FIXME: Implement
    public TimeSpan CurrentReadingTime => TimeSpan.Zero; // FIXME: Implement
    public ulong CurrentBytesRead => 0; // FIXME: Implement
    public ulong CurrentBytesWritten => 0; // FIXME: Implement

    public TimeSpan TotalIdlingTime => TimeSpan.Zero; // FIXME: Implement
    public TimeSpan TotalWritingTime => TimeSpan.Zero; // FIXME: Implement
    public TimeSpan TotalWaitingTime => TimeSpan.Zero; // FIXME: Implement
    public TimeSpan TotalReadingTime => TimeSpan.Zero; // FIXME: Implement
    public ulong TotalBytesRead => 0; // FIXME: Implement
    public ulong TotalBytesWritten => 0; // FIXME: Implement

    public Status CurrentStatus { get; private set; } = Status.Idling;

    internal ConnectionToServer(
        INegotiateRpcProtocol negotiateProtocol,
        IWriteMethodId writeMethodId,
        IReadMethodCallResult readMethodCallResult,
        RpcMetrics clientMetrics,
        RpcSocket socket)
    {
        mNegotiateProtocol = negotiateProtocol;
        mWriteMethodId = writeMethodId;
        mReadMethodCallResult = readMethodCallResult;
        mClientMetrics = clientMetrics;
        mRpcSocket = socket;
        
        mIdleStopwatch = new();
        mWaitStopwatch = new();
        mLog = RpcLoggerFactory.CreateLogger("ConnectionToServer");

        mConnectionId = mClientMetrics.ConnectionStart();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async Task ProcessMethodCallAsync(
        IMethodId methodId,
        RpcNetworkMessages messages,
        CancellationToken ct)
    {
        // This Task.Yield allows the client to fire the task and await it
        // at a later point without having to wait for all the synchronous
        // write operations.
        await Task.Yield();

        uint methodCallId = mClientMetrics.MethodCallStart();
        try
        {
            if (mRpc is null)
            {
                CurrentStatus = Status.NegotiatingProtocol;
                mRpc = await mNegotiateProtocol.NegotiateProtocolAsync(
                    mConnectionId,
                    Unsafe.As<IPEndPoint>(mRpcSocket.RemoteEndPoint)!,
                    mRpcSocket.Stream);
            }

            CurrentStatus = Status.Writing;
            mWriteMethodId.WriteMethodId(mRpc.Writer, methodId);
            messages.Request.Serialize(mRpc.Writer);

            CurrentStatus = Status.Waiting;
            mWaitStopwatch.Start();
            await mRpcSocket.WaitForDataAsync(ct);
            mWaitStopwatch.Reset();

            CurrentStatus = Status.Reading;
            MethodCallResult result = mReadMethodCallResult.Read(
                mRpc.Reader,
                out bool isResultAvailable,
                out Exception? ex);

            if (isResultAvailable)
                messages.Response.Deserialize(mRpc.Reader);

            if (ex is not null)
                throw ex;

            if (result == MethodCallResult.NotSupported)
            {
                throw new NotSupportedException(
                    $"Method {methodId} is not supported by the server");
            }
        }
        finally
        {
            mClientMetrics.MethodCallEnd();
        }
    }

    RpcProtocolNegotiationResult? mRpc;
    bool mDisposed;

    readonly uint mConnectionId;
    readonly INegotiateRpcProtocol mNegotiateProtocol;
    readonly IWriteMethodId mWriteMethodId;
    readonly IReadMethodCallResult mReadMethodCallResult;
    readonly RpcMetrics mClientMetrics;
    readonly RpcSocket mRpcSocket;
    readonly Stopwatch mIdleStopwatch;
    readonly Stopwatch mWaitStopwatch;
    readonly ILogger mLog;

    protected virtual void Dispose(bool disposing)
    {
        if (mDisposed)
            return;

        if (disposing)
        {
            mClientMetrics.ConnectionEnd();
            mRpcSocket.Close();
        }

        mDisposed = true;
    }
}

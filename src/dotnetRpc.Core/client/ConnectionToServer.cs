using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Core.Channels;
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
    public IPEndPoint RemoteEndPoint => mRpcChannel.RemoteEndPoint;

    public TimeSpan CurrentIdlingTime => mIdleStopwatch.Elapsed;
    public TimeSpan CurrentWritingTime => mRpcChannel.Stream.WriteTime - mLastWriteTime;
    public TimeSpan CurrentWaitingTime => mWaitStopwatch.Elapsed;
    public TimeSpan CurrentReadingTime => mRpcChannel.Stream.ReadTime - mLastReadTime;
    public ulong CurrentBytesRead => mRpcChannel.Stream.ReadBytes - mLastReadBytes;
    public ulong CurrentBytesWritten => mRpcChannel.Stream.WrittenBytes - mLastWrittenBytes;

    public TimeSpan TotalIdlingTime => mTotalIdlingTime + mIdleStopwatch.Elapsed;
    public TimeSpan TotalWritingTime => mRpcChannel.Stream.WriteTime;
    public TimeSpan TotalWaitingTime => mTotalWaitingTime + mWaitStopwatch.Elapsed;
    public TimeSpan TotalReadingTime => mRpcChannel.Stream.ReadTime;
    public ulong TotalBytesRead => mRpcChannel.Stream.ReadBytes;
    public ulong TotalBytesWritten => mRpcChannel.Stream.WrittenBytes;

    public Status CurrentStatus => mRpcChannel.IsConnected() ? mCurrentStatus : Status.Exited;

    internal ConnectionToServer(
        INegotiateRpcProtocol negotiateProtocol,
        IWriteMethodId writeMethodId,
        IReadMethodCallResult readMethodCallResult,
        RpcMetrics clientMetrics,
        IRpcChannel rpcChannel)
    {
        mNegotiateProtocol = negotiateProtocol;
        mWriteMethodId = writeMethodId;
        mReadMethodCallResult = readMethodCallResult;
        mClientMetrics = clientMetrics;
        mRpcChannel = rpcChannel;

        mConnectionId = mClientMetrics.ConnectionStart();
        mIdleStopwatch = new Stopwatch();
        mWaitStopwatch = new Stopwatch();
        mCallSemaphore = new SemaphoreSlim(1, 1);

        mIdleStopwatch.Start();
        mLog = RpcLoggerFactory.CreateLogger("ConnectionToServer");
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public bool IsConnected() => mRpcChannel.IsConnected();

    public async Task ProcessMethodCallAsync(
        IMethodId methodId,
        RpcNetworkMessages messages,
        CancellationToken ct)
    {
        // This Task.Yield allows the client to fire the task and await it
        // at a later point without having to wait for all the synchronous
        // write operations, in case the call semaphore returns a completed
        // task and no thread switch occurs.
        await Task.Yield();

        await mCallSemaphore.WaitAsync(ct);
        uint methodCallId = mClientMetrics.MethodCallStart();
        try
        {
            mIdleStopwatch.Stop();

            if (mRpc is null)
            {
                mCurrentStatus = Status.NegotiatingProtocol;
                mRpc = await mNegotiateProtocol.NegotiateProtocolAsync(
                    mConnectionId,
                    Unsafe.As<IPEndPoint>(mRpcChannel.RemoteEndPoint),
                    mRpcChannel.Stream);

                mLastReadBytes = mRpcChannel.Stream.ReadBytes;
                mLastWrittenBytes = mRpcChannel.Stream.WrittenBytes;
            }

            mCurrentStatus = Status.Writing;
            mWriteMethodId.WriteMethodId(mRpc.Writer, methodId);
            messages.Request.Serialize(mRpc.Writer);
            mRpc.Writer.Flush();

            mCurrentStatus = Status.Waiting;
            mWaitStopwatch.Start();
            await mRpcChannel.WaitForDataAsync(ct);
            mWaitStopwatch.Stop();

            mCurrentStatus = Status.Reading;
            MethodCallResult result = mReadMethodCallResult.Read(
                mRpc.Reader,
                out bool isResultAvailable,
                out RpcException? ex);

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
            TimeSpan callIdlingTime = mIdleStopwatch.Elapsed;
            TimeSpan callWritingTime = mRpcChannel.Stream.WriteTime - mLastWriteTime;
            TimeSpan callWaitingTime = mWaitStopwatch.Elapsed;
            TimeSpan callReadingTime = mRpcChannel.Stream.ReadTime - mLastReadTime;

            ulong callWrittenBytes = mRpcChannel.Stream.WrittenBytes - mLastWrittenBytes;
            ulong callReadBytes = mRpcChannel.Stream.ReadBytes - mLastReadBytes;

            mLog.LogTrace(
#pragma warning disable CA2017
                "Finished method call {1}{0}" +
                "Times{0}" +
                "  Idling: {2}, Writing: {3}, Waiting: {4}, Reading: {5}{0}" +
                "Bytes:{0}  Written: {6}, Read: {7}",
#pragma warning restore CA2017
                Environment.NewLine,
                methodCallId,
                callIdlingTime, callWritingTime, callWaitingTime, callReadingTime,
                callWrittenBytes, callReadBytes);

            mTotalIdlingTime += callIdlingTime;
            mTotalWaitingTime += callWaitingTime;

            mLastWrittenBytes = mRpcChannel.Stream.WrittenBytes;
            mLastReadBytes = mRpcChannel.Stream.ReadBytes;
            mLastWriteTime = mRpcChannel.Stream.WriteTime;
            mLastReadTime = mRpcChannel.Stream.ReadTime;

            mWaitStopwatch.Reset();

            mClientMetrics.MethodCallEnd();
            mIdleStopwatch.Restart();

            mCallSemaphore.Release();
        }
    }

    void Dispose(bool disposing)
    {
        if (mDisposed)
            return;

        if (disposing)
        {
            mClientMetrics.ConnectionEnd();
            mRpcChannel.Dispose();
            mCallSemaphore.Dispose();
        }

        mDisposed = true;
    }

    Status mCurrentStatus;
    RpcProtocolNegotiationResult? mRpc;
    TimeSpan mTotalIdlingTime = TimeSpan.Zero;
    TimeSpan mTotalWaitingTime = TimeSpan.Zero;
    ulong mLastReadBytes;
    TimeSpan mLastReadTime;
    ulong mLastWrittenBytes;
    TimeSpan mLastWriteTime;
    bool mDisposed;

    readonly uint mConnectionId;
    readonly INegotiateRpcProtocol mNegotiateProtocol;
    readonly IWriteMethodId mWriteMethodId;
    readonly IReadMethodCallResult mReadMethodCallResult;
    readonly RpcMetrics mClientMetrics;
    readonly IRpcChannel mRpcChannel;

    readonly Stopwatch mIdleStopwatch;
    readonly Stopwatch mWaitStopwatch;
    readonly ILogger mLog;

    readonly SemaphoreSlim mCallSemaphore;
}

using System;
using System.Diagnostics;
using System.Net;
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
    public IPEndPoint RemoteEndPoint => mRpcSocket.RemoteEndPoint;

    public TimeSpan CurrentIdlingTime => mIdleStopwatch.Elapsed;
    public TimeSpan CurrentWritingTime => mRpcSocket.Stream.WriteTime - mLastWriteTime;
    public TimeSpan CurrentWaitingTime => mWaitStopwatch.Elapsed;
    public TimeSpan CurrentReadingTime => mRpcSocket.Stream.ReadTime - mLastReadTime;
    public ulong CurrentBytesRead => mRpcSocket.Stream.ReadBytes - mLastReadBytes;
    public ulong CurrentBytesWritten => mRpcSocket.Stream.WrittenBytes - mLastWrittenBytes;

    public TimeSpan TotalIdlingTime => mTotalIdlingTime + mIdleStopwatch.Elapsed;
    public TimeSpan TotalWritingTime => mRpcSocket.Stream.WriteTime;
    public TimeSpan TotalWaitingTime => mTotalWaitingTime + mWaitStopwatch.Elapsed;
    public TimeSpan TotalReadingTime => mRpcSocket.Stream.ReadTime;
    public ulong TotalBytesRead => mRpcSocket.Stream.ReadBytes;
    public ulong TotalBytesWritten => mRpcSocket.Stream.WrittenBytes;

    public Status CurrentStatus => mRpcSocket.IsConnected() ? mCurrentStatus : Status.Exited;

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
        mConnectionId = mClientMetrics.ConnectionStart();

        mIdleStopwatch.Start();
        mLog = RpcLoggerFactory.CreateLogger("ConnectionToServer");
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public bool IsConnected() => mRpcSocket.IsConnected();

    public async Task ProcessMethodCallAsync(
        IMethodId methodId,
        RpcNetworkMessages messages,
        CancellationToken ct)
    {
        // This Task.Yield allows the client to fire the task and await it
        // at a later point without having to wait for all the synchronous
        // write operations.
        await Task.Yield();
        mIdleStopwatch.Stop();

        uint methodCallId = mClientMetrics.MethodCallStart();
        try
        {
            if (mRpc is null)
            {
                mCurrentStatus = Status.NegotiatingProtocol;
                mRpc = await mNegotiateProtocol.NegotiateProtocolAsync(
                    mConnectionId,
                    Unsafe.As<IPEndPoint>(mRpcSocket.RemoteEndPoint)!,
                    mRpcSocket.Stream);

                mLastReadBytes = mRpcSocket.Stream.ReadBytes;
                mLastWrittenBytes = mRpcSocket.Stream.WrittenBytes;
            }

            mCurrentStatus = Status.Writing;
            mWriteMethodId.WriteMethodId(mRpc.Writer, methodId);
            messages.Request.Serialize(mRpc.Writer);
            mRpc.Writer.Flush();

            mCurrentStatus = Status.Waiting;
            mWaitStopwatch.Start();
            await mRpcSocket.WaitForDataAsync(ct);
            mWaitStopwatch.Stop();

            mCurrentStatus = Status.Reading;
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
            TimeSpan callIdlingTime = mIdleStopwatch.Elapsed;
            TimeSpan callWritingtime = mRpcSocket.Stream.WriteTime - mLastWriteTime;
            TimeSpan callWaitingTime = mWaitStopwatch.Elapsed;
            TimeSpan callReadingTime = mRpcSocket.Stream.ReadTime - mLastReadTime;

            ulong callWrittenBytes = mRpcSocket.Stream.WrittenBytes - mLastWrittenBytes;
            ulong callReadBytes = mRpcSocket.Stream.ReadBytes - mLastReadBytes;

            mLog.LogTrace(
                "Finished method call {1}{0}" +
                "Times{0}" +
                "  Idling: {2}, Writing: {3}, Waiting: {4}, Reading: {5}{0}" +
                "Bytes:{0}" +
                "  Written: {6}, Read: {7}",
                Environment.NewLine,
                methodCallId,
                callIdlingTime, callWritingtime, callWaitingTime, callReadingTime,
                callWrittenBytes, callReadBytes);

            mTotalIdlingTime += callIdlingTime;
            mTotalWaitingTime += callWaitingTime;

            mLastWrittenBytes = mRpcSocket.Stream.WrittenBytes;
            mLastReadBytes = mRpcSocket.Stream.ReadBytes;
            mLastWriteTime = mRpcSocket.Stream.WriteTime;
            mLastReadTime = mRpcSocket.Stream.ReadTime;

            mWaitStopwatch.Reset();

            mClientMetrics.MethodCallEnd();
            mIdleStopwatch.Restart();
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (mDisposed)
            return;

        if (disposing)
        {
            mClientMetrics.ConnectionEnd();
            mRpcSocket.Dispose();
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
    readonly RpcSocket mRpcSocket;
    
    readonly Stopwatch mIdleStopwatch;
    readonly Stopwatch mWaitStopwatch;
    readonly ILogger mLog;
}

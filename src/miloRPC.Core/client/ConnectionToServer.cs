using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using miloRPC.Core.Channels;
using miloRPC.Core.Shared;

namespace miloRPC.Core.Client;

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

    public IConnectionContext Context => mContext;

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

    public ConnectionToServer(
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

        mIdleStopwatch = new Stopwatch();
        mWaitStopwatch = new Stopwatch();
        mCallSemaphore = new SemaphoreSlim(1, 1);

        mContext = new ConnectionContext(
            mClientMetrics.ConnectionStart(),
            mRpcChannel.ChannelProtocol,
            mRpcChannel.LocalEndPoint,
            mRpcChannel.RemoteEndPoint);

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
        bool methodCallFinished = false;

        await mCallSemaphore.WaitAsync(ct);
        uint methodCallId = mClientMetrics.MethodCallStart();
        try
        {
            mIdleStopwatch.Stop();

            if (mRpc is null)
            {
                mCurrentStatus = Status.NegotiatingProtocol;
                mRpc = await mNegotiateProtocol.NegotiateProtocolAsync(
                    mContext, mRpcChannel.Stream);

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
                out SerializableException? ex);

            if (isResultAvailable)
                messages.Response.Deserialize(mRpc.Reader);

            if (ex is not null)
                throw ex;

            if (result == MethodCallResult.NotSupported)
            {
                methodCallFinished = true;
                EndOfDataSequence.ProcessFromClient(mRpc.Writer, mRpc.Reader);
                throw new NotSupportedException(
                    $"Method {methodId} is not supported by the server");
            }

            if (messages.Response is not DestinationStreamMessage m)
            {
                methodCallFinished = true;
                return;
            }

            mLog.LogWarning(
                "Connection is processing stream-oriented method call {MethodId}, " +
                "setting up disposing actions...",
                methodId);

            var cappedNetworkStream =
                Unsafe.As<DestinationStreamMessage.CappedNetworkStream>(m.Stream);

            // Ours is the most important dispose action and must be run first,
            // despite of what the user might've set up
            cappedNetworkStream.SuccessfulDisposeActions.Insert(0, () =>
            {
                mLog.LogDebug(
                    "Stream-oriented method call {MethodId} finished!",
                    methodId);
                UpdateMetricsAfterMethodCall(methodCallId);
                mCurrentStatus = Status.Idling;
                mCallSemaphore.Release();
            });

            ConnectionToServer thisConn = this;
            cappedNetworkStream.FailedDisposeAction = () =>
            {
                mLog.LogCritical(
                    "Stream-oriented method call {MethodId} disposed the stream " +
                    "without consuming it!",
                    methodId);
                thisConn.Dispose();
            };
        }
        finally
        {
            if (methodCallFinished)
            {
                UpdateMetricsAfterMethodCall(methodCallId);
                mCurrentStatus = Status.Idling;
                mCallSemaphore.Release();
            }
        }
    }

    void UpdateMetricsAfterMethodCall(uint methodCallId)
    {
        TimeSpan callIdlingTime = mIdleStopwatch.Elapsed;
        TimeSpan callWritingTime = mRpcChannel.Stream.WriteTime - mLastWriteTime;
        TimeSpan callWaitingTime = mWaitStopwatch.Elapsed;
        TimeSpan callReadingTime = mRpcChannel.Stream.ReadTime - mLastReadTime;

        ulong callWrittenBytes = mRpcChannel.Stream.WrittenBytes - mLastWrittenBytes;
        ulong callReadBytes = mRpcChannel.Stream.ReadBytes - mLastReadBytes;

        mLog.LogTrace(
            "T {MethodCallId} > Idling: {IdlingTimeMs}ms | Writing: {WritingTimeMs}ms " +
            "| Waiting: {WaitingTimeMs}ms | Reading: {ReadingTimeMs}ms ",
            methodCallId,
            callIdlingTime.TotalMilliseconds,
            callWritingTime.TotalMilliseconds,
            callWaitingTime.TotalMilliseconds,
            callReadingTime.TotalMilliseconds);
        mLog.LogTrace(
            "B {MethodCallId} > Written: {WrittenBytes} | Read: {ReadBytes}",
            methodCallId, callWrittenBytes, callReadBytes);

        mTotalIdlingTime += callIdlingTime;
        mTotalWaitingTime += callWaitingTime;

        mLastWrittenBytes = mRpcChannel.Stream.WrittenBytes;
        mLastReadBytes = mRpcChannel.Stream.ReadBytes;
        mLastWriteTime = mRpcChannel.Stream.WriteTime;
        mLastReadTime = mRpcChannel.Stream.ReadTime;

        mWaitStopwatch.Reset();

        mClientMetrics.MethodCallEnd(callReadBytes, callWrittenBytes);
        mIdleStopwatch.Restart();
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

    readonly INegotiateRpcProtocol mNegotiateProtocol;
    readonly IWriteMethodId mWriteMethodId;
    readonly IReadMethodCallResult mReadMethodCallResult;
    readonly RpcMetrics mClientMetrics;
    readonly IRpcChannel mRpcChannel;
    readonly ConnectionContext mContext;

    readonly Stopwatch mIdleStopwatch;
    readonly Stopwatch mWaitStopwatch;
    readonly ILogger mLog;

    readonly SemaphoreSlim mCallSemaphore;
}

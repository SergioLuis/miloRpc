using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Core.Channels;
using dotnetRpc.Core.Extensions;
using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Server;

public class ConnectionFromClient
{
    public enum Status
    {
        Idling,
        NegotiatingProtocol,
        Reading,
        Running,
        Writing,
        Exited
    }

    public uint ConnectionId => mConnectionId;
    public IPEndPoint RemoteEndPoint => mRpcChannel.RemoteEndPoint;

    public TimeSpan CurrentIdlingTime => mIdleStopwatch.Elapsed;
    public TimeSpan CurrentRunningTime => mRunStopwatch.Elapsed;
    public TimeSpan CurrentReadingTime => mRpcChannel.Stream.ReadTime - mLastReadTime;
    public TimeSpan CurrentWritingTime => mRpcChannel.Stream.WriteTime - mLastWriteTime;
    public ulong CurrentBytesRead => mRpcChannel.Stream.ReadBytes - mLastReadBytes;
    public ulong CurrentBytesWritten => mRpcChannel.Stream.WrittenBytes - mLastWrittenBytes;

    public TimeSpan TotalIdlingTime => mTotalIdlingTime + mIdleStopwatch.Elapsed;
    public TimeSpan TotalRunningTime => mTotalRunningTime + mRunStopwatch.Elapsed;
    public TimeSpan TotalReadingTime => mRpcChannel.Stream.ReadTime;
    public TimeSpan TotalWritingTime => mRpcChannel.Stream.WriteTime;
    public ulong TotalBytesRead => mRpcChannel.Stream.ReadBytes;
    public ulong TotalBytesWritten => mRpcChannel.Stream.WrittenBytes;

    public Status CurrentStatus { get; private set; }

    internal ConnectionFromClient(
        StubCollection stubCollection,
        INegotiateRpcProtocol negotiateProtocol,
        IReadMethodId readMethodId,
        IWriteMethodCallResult writeMethodCallResult,
        RpcMetrics serverMetrics,
        IRpcChannel rpcChannel,
        ConnectionTimeouts connectionTimeouts)
    {
        mStubCollection = stubCollection;
        mNegotiateProtocol = negotiateProtocol;
        mReadMethodId = readMethodId;
        mWriteMethodCallResult = writeMethodCallResult;
        mServerMetrics = serverMetrics;
        mRpcChannel = rpcChannel;
        mConnectionTimeouts = connectionTimeouts;

        mIdleStopwatch = new Stopwatch();
        mRunStopwatch = new Stopwatch();
        mConnectionId = mServerMetrics.ConnectionStart();

        mLog = RpcLoggerFactory.CreateLogger("ConnectionFromClient");
    }

    public bool IsConnected() => mRpcChannel.IsConnected();

    internal async ValueTask ProcessConnMessagesLoop(CancellationToken ct)
    {
        CancellationToken idlingCt = CancellationToken.None;
        CancellationToken runningCt = CancellationToken.None;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                CurrentStatus = Status.Idling;

                mIdleStopwatch.Start();
                idlingCt = ct.CancelLinkedTokenAfter(mConnectionTimeouts.Idling);
                await mRpcChannel.WaitForDataAsync(idlingCt);
                idlingCt = CancellationToken.None;
                mIdleStopwatch.Stop();

                uint methodCallId = mServerMetrics.MethodCallStart();
                try
                {
                    if (mRpc is null)
                    {
                        CurrentStatus = Status.NegotiatingProtocol;
                        mRpc = await mNegotiateProtocol.NegotiateProtocolAsync(
                            mConnectionId,
                            mRpcChannel.RemoteEndPoint,
                            mRpcChannel.Stream);

                        mLastReadBytes = mRpcChannel.Stream.ReadBytes;
                        mLastWrittenBytes = mRpcChannel.Stream.WrittenBytes;
                        mLastReadTime = mRpcChannel.Stream.ReadTime;
                        mLastWriteTime = mRpcChannel.Stream.WriteTime;
                    }

                    CurrentStatus = Status.Reading;
                    IMethodId methodId = mReadMethodId.ReadMethodId(mRpc.Reader);
                    methodId.SetSolvedMethodName(mStubCollection.SolveMethodName(methodId));

                    IStub? stub = mStubCollection.FindStub(methodId);
                    if (stub == null)
                    {
                        mLog.LogWarning(
                            "Client tried to run an unsupported method (connId {ConnectionId}): {MethodId}",
                            mConnectionId, methodId);
                        mWriteMethodCallResult.Write(mRpc.Writer, MethodCallResult.NotSupported);
                        return;
                    }

                    Func<CancellationToken> beginMethodRunCallback = () =>
                    {
                        CurrentStatus = Status.Running;
                        mRunStopwatch.Start();
                        runningCt = ct.CancelLinkedTokenAfter(mConnectionTimeouts.Running);
                        return runningCt;
                    };

                    RpcNetworkMessages messages =
                        await stub.RunMethodCallAsync(methodId, mRpc.Reader, beginMethodRunCallback);
                    runningCt = CancellationToken.None;
                    mRunStopwatch.Stop();

                    CurrentStatus = Status.Writing;
                    mWriteMethodCallResult.Write(mRpc.Writer, MethodCallResult.Ok);
                    messages.Response.Serialize(mRpc.Writer);
                    mRpc.Writer.Flush();

                    // ReSharper disable once SuspiciousTypeConversion.Global
                    if (messages.Request is IDisposable disposableRequest)
                        disposableRequest.Dispose();

                    // ReSharper disable once SuspiciousTypeConversion.Global
                    if (messages.Response is IDisposable disposableResponse)
                        disposableResponse.Dispose();

                    ct.ThrowIfCancellationRequested();
                }
                catch (Exception ex)
                {
                    if (ct.IsCancellationRequested)
                        mLog.LogError("The general CancellationToken was cancelled");

                    if (idlingCt.IsCancellationRequested)
                        mLog.LogError("The Idling CancellationToken was cancelled");

                    if (runningCt.IsCancellationRequested)
                        mLog.LogError("The Running CancellationToken was cancelled");

                    if (mRpc is null)
                    {
                        mLog.LogError(
                            "Caught an exception but the protocol is not " +
                            "negotiated yet, rethrowing...");
                        throw;
                    }

                    if (!mRpcChannel.IsConnected())
                    {
                        mLog.LogError(
                            "Caught an exception but the RpcSocket is not " +
                            "connected, rethrowing...");
                        throw;
                    }

                    if (!mRpc.Writer.BaseStream.CanWrite
                        || !mRpc.Reader.BaseStream.CanRead)
                    {
                        mLog.LogError(
                            "Caught an exception but can't write to nor read from "
                            + "the underlying stream, rethrowing...");
                        throw;
                    }

                    if (CurrentStatus is Status.Reading or Status.Running)
                    {
                        mLog.LogInformation(
                            "Caught an exception while running a method and the " +
                            "client is still connected, sending the exception as " +
                            "a failed method call result");
                        mWriteMethodCallResult.Write(
                            mRpc.Writer, MethodCallResult.Failed, RpcException.FromException(ex));
                    }
                }
                finally
                {
                    TimeSpan callIdlingTime = mIdleStopwatch.Elapsed;
                    TimeSpan callReadingTime = mRpcChannel.Stream.ReadTime - mLastReadTime;
                    TimeSpan callRunningTime = mRunStopwatch.Elapsed;
                    TimeSpan callWritingTime = mRpcChannel.Stream.WriteTime - mLastWriteTime;

                    ulong callReadBytes = mRpcChannel.Stream.ReadBytes - mLastReadBytes;
                    ulong callWrittenBytes = mRpcChannel.Stream.WrittenBytes - mLastWrittenBytes;

                    mLog.LogTrace(
                        "T {MethodCallId} | Idling: {IdlingTimeMs}ms. | Reading: {ReadingTimeMs}ms. " +
                        "| Running: {RunningTimeMs}ms. | Writing: {WritingTimeMs}ms.",
                        methodCallId, callIdlingTime, callReadingTime, callRunningTime, callWritingTime);
                    mLog.LogTrace(
                        "B {MethodCallId} | Read: {ReadBytes} | Written: {WrittenBytes}",
                        methodCallId, callReadBytes, callWrittenBytes);

                    mTotalIdlingTime += callIdlingTime;
                    mTotalRunningTime += callRunningTime;

                    mLastReadBytes = mRpcChannel.Stream.ReadBytes;
                    mLastWrittenBytes = mRpcChannel.Stream.WrittenBytes;
                    mLastReadTime = mRpcChannel.Stream.ReadTime;
                    mLastWriteTime = mRpcChannel.Stream.WriteTime;

                    mIdleStopwatch.Reset();
                    mRunStopwatch.Reset();

                    mServerMetrics.MethodCallEnd();
                }
            }
        }
        catch (Exception ex)
        {
            mLog.LogError(
                "Caught an exception not handled by ProcessConnMessagesLoop, " +
                "the connection is going to exit");
            mLog.LogError("Type: {ExType}, Message: {ExMessage}", ex.GetType(), ex.Message);
            mLog.LogDebug("StackTrace:\r\n{ExStackTrace}", ex.StackTrace);
        }
        finally
        {
            CurrentStatus = Status.Exited;
            mServerMetrics.ConnectionEnd();
            mRpcChannel.Dispose();
        }

        mLog.LogTrace("ProcessConnMessagesLoop completed");
    }

    RpcProtocolNegotiationResult? mRpc;
    TimeSpan mTotalIdlingTime = TimeSpan.Zero;
    TimeSpan mTotalRunningTime = TimeSpan.Zero;
    ulong mLastReadBytes;
    TimeSpan mLastReadTime;
    ulong mLastWrittenBytes;
    TimeSpan mLastWriteTime;

    readonly uint mConnectionId;
    readonly StubCollection mStubCollection;
    readonly INegotiateRpcProtocol mNegotiateProtocol;
    readonly IReadMethodId mReadMethodId;
    readonly IWriteMethodCallResult mWriteMethodCallResult;
    readonly RpcMetrics mServerMetrics;
    readonly IRpcChannel mRpcChannel;
    readonly ConnectionTimeouts mConnectionTimeouts;

    readonly Stopwatch mIdleStopwatch;
    readonly Stopwatch mRunStopwatch;
    readonly ILogger mLog;
}

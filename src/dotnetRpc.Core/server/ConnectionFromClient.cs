using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
    public IPEndPoint RemoteEndPoint => mRpcSocket.RemoteEndPoint;

    public TimeSpan CurrentIdlingTime => mIdleStopwatch.Elapsed;
    public TimeSpan CurrentRunningTime => mRunStopwatch.Elapsed;
    public TimeSpan CurrentReadingTime => mRpcSocket.Stream.ReadTime - mLastReadTime;
    public TimeSpan CurrentWritingTime => mRpcSocket.Stream.WriteTime - mLastWriteTime;
    public ulong CurrentBytesRead => mRpcSocket.Stream.ReadBytes - mLastReadBytes;
    public ulong CurrentBytesWritten => mRpcSocket.Stream.WrittenBytes - mLastWrittenBytes;

    public TimeSpan TotalIdlingTime => mTotalIdlingTime + mIdleStopwatch.Elapsed;
    public TimeSpan TotalRunningTime => mTotalRunningTime + mRunStopwatch.Elapsed;
    public TimeSpan TotalReadingTime => mRpcSocket.Stream.ReadTime;
    public TimeSpan TotalWrittingTime => mRpcSocket.Stream.WriteTime;
    public ulong TotalBytesRead => mRpcSocket.Stream.ReadBytes;
    public ulong TotalBytesWritten => mRpcSocket.Stream.WrittenBytes;

    public Status CurrentStatus { get; private set; }

    internal ConnectionFromClient(
        StubCollection stubCollection,
        INegotiateRpcProtocol negotiateProtocol,
        IReadMethodId readMethodId,
        IWriteMethodCallResult writeMethodCallResult,
        RpcMetrics serverMetrics,
        RpcSocket socket,
        ConnectionTimeouts connectionTimeouts)
    {
        mStubCollection = stubCollection;
        mNegotiateProtocol = negotiateProtocol;
        mReadMethodId = readMethodId;
        mWriteMethodCallResult = writeMethodCallResult;
        mServerMetrics = serverMetrics;
        mRpcSocket = socket;
        mConnectionTimeouts = connectionTimeouts;

        mIdleStopwatch = new();
        mRunStopwatch = new();
        mConnectionId = mServerMetrics.ConnectionStart();

        mLog = RpcLoggerFactory.CreateLogger("ConnectionFromClient");
    }

    public bool IsConnected() => mRpcSocket.IsConnected();

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
                await mRpcSocket.WaitForDataAsync(idlingCt);
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
                            mRpcSocket.RemoteEndPoint,
                            mRpcSocket.Stream);

                        mLastReadBytes = mRpcSocket.Stream.ReadBytes;
                        mLastWrittenBytes = mRpcSocket.Stream.WrittenBytes;
                        mLastReadTime = mRpcSocket.Stream.ReadTime;
                        mLastWriteTime = mRpcSocket.Stream.WriteTime;
                    }

                    CurrentStatus = Status.Reading;
                    mLastReadBytes = mRpcSocket.Stream.ReadBytes;                  
                    IMethodId methodId = mReadMethodId.ReadMethodId(mRpc.Reader);
                    methodId.SetSolvedMethodName(mStubCollection.SolveMethodName(methodId));

                    IStub? stub = mStubCollection.FindStub(methodId);
                    if (stub == null)
                    {
                        mLog.LogWarning(
                            "Client tried to run an unsupport method (connId {0}): {1}",
                            mConnectionId, methodId);
                        mWriteMethodCallResult.Write(mRpc.Writer, MethodCallResult.NotSupported);
                        continue;
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
                    mWriteMethodCallResult.Write(mRpc.Writer, MethodCallResult.OK);
                    messages.Response.Serialize(mRpc.Writer);
                    mRpc.Writer.Flush();

                    if (messages.Request is IDisposable disposableRequest)
                        disposableRequest.Dispose();

                    if (messages.Response is IDisposable disposableResponse)
                        disposableResponse.Dispose();

                    ct.ThrowIfCancellationRequested();
                }
                catch (Exception ex)
                {
                    if (mRpc is null)
                    {
                        mLog.LogError(
                            "Caught an exception but the protocol is not " +
                            "negotiated yet, rethrowing...");
                        throw;
                    }

                    if (!mRpcSocket.IsConnected())
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

                    if (CurrentStatus == Status.Reading
                        || CurrentStatus == Status.Running)
                    {
                        mLog.LogInformation(
                            "Caught an exception while running a method and the " +
                            "client is still connected, sending the exception as " +
                            "a failed method call result");
                        mWriteMethodCallResult.Write(mRpc.Writer, MethodCallResult.Failed, ex);
                    }
                }
                finally
                {
                    TimeSpan callIdlingTime = mIdleStopwatch.Elapsed;
                    TimeSpan callReadingTime = mRpcSocket.Stream.ReadTime - mLastReadTime;
                    TimeSpan callRunningTime = mRunStopwatch.Elapsed;
                    TimeSpan callWrittingTime = mRpcSocket.Stream.WriteTime - mLastWriteTime;

                    ulong callReadBytes = mRpcSocket.Stream.ReadBytes - mLastReadBytes;
                    ulong callWrittenBytes = mRpcSocket.Stream.WrittenBytes - mLastWrittenBytes;

                    mLog.LogTrace(
                        "Finished method call {1}{0}" +
                        "Times{0}" +
                        "  Idling: {2}, Reading: {3}, Running: {4}, Writting: {5}{0}" +
                        "Bytes:{0}" +
                        "  Read: {6}, Written: {7}",
                        Environment.NewLine,
                        methodCallId,
                        callIdlingTime, callReadingTime, callRunningTime, callWrittingTime,
                        callReadBytes, callWrittenBytes);


                    mTotalIdlingTime += callIdlingTime;
                    mTotalRunningTime += callRunningTime;

                    mLastReadBytes = mRpcSocket.Stream.ReadBytes;
                    mLastWrittenBytes = mRpcSocket.Stream.WrittenBytes;
                    mLastReadTime = mRpcSocket.Stream.ReadTime;
                    mLastWriteTime = mRpcSocket.Stream.WriteTime;

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
            mLog.LogError("Type: {0}, Message: {1}", ex.GetType(), ex.Message);
            mLog.LogDebug("StackTrace:{0}{1}", Environment.NewLine, ex.StackTrace);
        }
        finally
        {
            CurrentStatus = Status.Exited;
            mServerMetrics.ConnectionEnd();
            mRpcSocket.Dispose();
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
    readonly RpcSocket mRpcSocket;
    readonly ConnectionTimeouts mConnectionTimeouts;

    readonly Stopwatch mIdleStopwatch;
    readonly Stopwatch mRunStopwatch;
    readonly ILogger mLog;
}

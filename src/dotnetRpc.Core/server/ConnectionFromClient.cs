using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
    public TimeSpan CurrentReadingTime => TimeSpan.Zero; // FIXME: Implement
    public TimeSpan CurrentWritingTime => TimeSpan.Zero; // FIXME: Implement
    public ulong CurrentBytesRead => 0; // FIXME: Implement
    public ulong CurrentBytesWritten => 0; // FIXME: Implement

    public TimeSpan TotalIdlingTime => TimeSpan.Zero; // FIXME: Implement
    public TimeSpan TotalRunningTime => TimeSpan.Zero; // FIXME: Implement
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
        mLog = RpcLoggerFactory.CreateLogger("ConnectionFromClient");

        mConnectionId = mServerMetrics.ConnectionStart();
    }

    internal bool IsRpcSocketConnected() => mRpcSocket.IsConnected();

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
                mIdleStopwatch.Reset();

                try
                {
                    uint methodCallId = mServerMetrics.MethodCallStart();

                    if (mRpc is null)
                    {
                        CurrentStatus = Status.NegotiatingProtocol;
                        mRpc = await mNegotiateProtocol.NegotiateProtocolAsync(
                            mConnectionId,
                            mRpcSocket.RemoteEndPoint,
                            mRpcSocket.Stream);
                    }

                    CurrentStatus = Status.Reading;
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
                    mRunStopwatch.Reset();

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
                    mRunStopwatch.Reset();
                    mIdleStopwatch.Reset();

                    if (mRpc is null)
                        throw;

                    if (!mRpcSocket.IsConnected())
                        throw;

                    if (!mRpc.Writer.BaseStream.CanWrite
                        || !mRpc.Reader.BaseStream.CanRead)
                    {
                        throw;
                    }

                    if (CurrentStatus == Status.Reading
                        || CurrentStatus == Status.Running)
                    {
                        mWriteMethodCallResult.Write(mRpc.Writer, MethodCallResult.Failed, ex);
                    }
                }
                finally
                {
                    mServerMetrics.MethodCallEnd();
                }
            }
        }
        catch (SocketException ex)
        {
            // Most probably the client closed the connection
        }
        catch (EndOfStreamException ex)
        {
            if (mRpc is null)
                return;

            if (!mRpcSocket.IsConnected())
                return;

            if (!mRpc.Writer.BaseStream.CanWrite
                || !mRpc.Reader.BaseStream.CanRead)
            {
                return;
            }
        }
        finally
        {
            CurrentStatus = Status.Exited;
            mServerMetrics.ConnectionEnd();
            mRpcSocket.Close();
        }

        mLog.LogTrace("ProcessConnMessagesLoop completed");
    }

    async Task<string> ProcessEchoRequestAsync(
        BinaryReader reader, BinaryWriter writer, CancellationToken ct)
    {
        string echoRequest = reader.ReadString();

        CurrentStatus = Status.Running;
        await Task.Delay(300, ct);

        string reply = $"Reply: {echoRequest}";
        return reply;
    }

    RpcProtocolNegotiationResult? mRpc;
    TimeSpan mTotalIdlingTime = TimeSpan.Zero;
    TimeSpan mTotalRunningTime = TimeSpan.Zero;

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

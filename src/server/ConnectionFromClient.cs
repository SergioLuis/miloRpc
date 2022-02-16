using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Extensions;
using dotnetRpc.Shared;

namespace dotnetRpc.Server;

internal class ConnectionFromClient
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
    public ulong TotalBytesRead => mRpcSocket.Stream.ReadBytes;
    public ulong TotalBytesWritten => mRpcSocket.Stream.WrittenBytes;
    public TimeSpan TotalTimeReading => mRpcSocket.Stream.ReadTime;
    public TimeSpan TotalTimeWritting => mRpcSocket.Stream.WriteTime;

    public Status CurrentStatus { get; private set; }

    internal ConnectionFromClient(
        INegotiateRpcProtocol negotiateProtocol,
        RpcMetrics serverMetrics,
        RpcSocket socket,
        ConnectionTimeouts connectionTimeouts)
    {
        mNegotiateProtocol = negotiateProtocol;
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
                    byte method = mRpc.Reader.ReadByte();

                    switch (method)
                    {
                        case ((byte)255):
                            mRunStopwatch.Start();
                            runningCt = ct.CancelLinkedTokenAfter(mConnectionTimeouts.Running);
                            string result = await ProcessEchoRequestAsync(
                                mRpc.Reader, mRpc.Writer, runningCt);
                            runningCt = CancellationToken.None;
                            mRunStopwatch.Reset();

                            CurrentStatus = Status.Writing;
                            mRpc.Writer.Write(result);
                            break;


                        default:
                            // What happens if we invoked a non-supported method...?
                            break;
                    }

                    mRpc.Writer.Flush();

                    ct.ThrowIfCancellationRequested();
                }
                finally
                {
                    mServerMetrics.MethodCallEnd();
                }
            }
        }
        catch (OperationCanceledException) when (
            ct.IsCancellationRequested
            || idlingCt.IsCancellationRequested
            || runningCt.IsCancellationRequested)
        {
            // Nothing to do, a timeout expired or the server is being shut down
        }
        catch (SocketException ex)
        {
            // Most probably the client closed the connection
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

    readonly uint mConnectionId;
    readonly INegotiateRpcProtocol mNegotiateProtocol;
    readonly RpcMetrics mServerMetrics;
    readonly RpcSocket mRpcSocket;
    readonly ConnectionTimeouts mConnectionTimeouts;
    readonly Stopwatch mIdleStopwatch;
    readonly Stopwatch mRunStopwatch;
    readonly ILogger mLog;
}

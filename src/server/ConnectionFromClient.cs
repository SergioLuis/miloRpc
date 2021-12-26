using System;
using System.Diagnostics;
using System.IO;
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
        Reading,
        Running,
        Writing,
        Exited
    }

    public uint ConnectionId => mConnectionId;
    public TimeSpan CurrentIdlingTime => mIdleStopwatch.Elapsed;
    public TimeSpan CurrentRunningTime => mRunStopwatch.Elapsed;
    public ulong TotalBytesRead => mRpcSocket.Stream.ReadBytes;
    public ulong TotalBytesWritten => mRpcSocket.Stream.WrittenBytes;
    public TimeSpan TotalTimeReading => mRpcSocket.Stream.ReadTime;
    public TimeSpan TotalTimeWritting => mRpcSocket.Stream.WriteTime;

    public Status CurrentStatus { get; private set; }

    internal ConnectionFromClient(
        uint connectionId,
        INegotiateRpcProtocol negotiateProtocol,
        RpcMetrics serverMetrics,
        RpcSocket socket,
        int idlingTimeoutMillis,
        int runningTimeoutMillis)
    {
        mConnectionId = connectionId;
        mServerMetrics = serverMetrics;
        mRpcSocket = socket;
        mIdleTimeoutMillis = idlingTimeoutMillis;
        mRunTimeoutMillis = runningTimeoutMillis;
        mIdleStopwatch = new();
        mRunStopwatch = new();
        mLog = RpcLoggerFactory.CreateLogger("ConnectionFromClient");
    }

    internal bool IsRpcSocketConnected() => mRpcSocket.IsConnected();

    internal async ValueTask ProcessConnMessagesLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                CurrentStatus = Status.Idling;

                mIdleStopwatch.Start();
                await mRpcSocket.WaitForDataAsync(
                    ct.CancelAfterTimeout(mIdleTimeoutMillis));
                mIdleStopwatch.Reset();

                try
                {
                    uint methodCallId = mServerMetrics.MethodCallStart();
                    await ProcessMethodCall(methodCallId, ct);
                }
                finally
                {
                    mServerMetrics.MethodCallEnd();
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            // The server is exiting - nothing to do for now
        }
        catch (SocketException ex)
        {
            // Most probably the client closed the connection
        }
        catch (Exception ex)
        {
            throw;
        }
        finally
        {
            CurrentStatus = Status.Exited;
            mServerMetrics.ConnectionEnd();
        }

        mLog.LogTrace("ProcessConnMessagesLoop completed");
    }

    async Task ProcessMethodCall(uint methodCallId, CancellationToken ct)
    {
        mReader ??= new(mRpcSocket.Stream);
        mWriter ??= new(mRpcSocket.Stream);

        byte[] protocolCapabilities = new byte[8];

        CurrentStatus = Status.Reading;

        byte protocolVersion = mReader.ReadByte();
        mReader.Read(protocolCapabilities, 0, 8);

        CurrentStatus = Status.Writing;
        mWriter.Write(protocolCapabilities); // For now we return the same capabilities we read
        mWriter.Flush();

        CurrentStatus = Status.Reading;
        byte method = mReader.ReadByte();

        // All of this must be tidied up...
        CurrentStatus = Status.Running;
        switch (method)
        {
            case ((byte)255):
                string result = await ProcessEchoRequest(
                    mReader, mWriter, ct.CancelAfterTimeout(mRunTimeoutMillis));
                CurrentStatus = Status.Writing;
                mWriter.Write(result);
                break;


            default:
                // What happens if we invoked a non-supported method...?
                break;
        }

        mWriter.Flush();

        ct.ThrowIfCancellationRequested();
    }

    static async Task<string> ProcessEchoRequest(
        BinaryReader reader, BinaryWriter writer, CancellationToken ct)
    {
        string echoRequest = reader.ReadString();

        await Task.Delay(300);

        string reply = $"Reply: {echoRequest}";
        return reply;
    }

    BinaryReader? mReader;
    BinaryWriter? mWriter;

    readonly uint mConnectionId;
    readonly RpcMetrics mServerMetrics;
    readonly RpcSocket mRpcSocket;
    readonly int mIdleTimeoutMillis;
    readonly int mRunTimeoutMillis;
    readonly Stopwatch mIdleStopwatch;
    readonly Stopwatch mRunStopwatch;
    readonly ILogger mLog;
}

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Shared;

namespace dotnetRpc.Server;

internal class ConnectionFromClient
{
    public TimeSpan IdleTime => mIdleStopwatch.Elapsed;

    internal ConnectionFromClient(RpcMetrics serverMetrics, RpcSocket socket)
    {
        mServerMetrics = serverMetrics;
        mRpcSocket = socket;
        mIdleStopwatch = new();
        mLog = RpcLoggerFactory.CreateLogger("ConnectionFromClient");
    }

    internal async ValueTask ProcessConnMessagesLoop(CancellationToken ct)
    {
        uint connectionId = mServerMetrics.ConnectionStart();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                mIdleStopwatch.Start();
                await mRpcSocket.WaitForDataAsync(ct);
                mIdleStopwatch.Reset();
                try
                {
                    uint methodCallId = mServerMetrics.MethodCallStart();
                    ProcessMethodCall(connectionId, methodCallId, ct);
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
            mServerMetrics.ConnectionEnd();
        }

        mLog.LogTrace("ProcessConnMessagesLoop completed");
    }

    void ProcessMethodCall(uint connectionId, uint methodCallId, CancellationToken ct)
    {
        mReader ??= new(mRpcSocket.Stream);
        mWriter ??= new(mRpcSocket.Stream);

        byte[] protocolCapabilities = new byte[8];

        byte protocolVersion = mReader.ReadByte();
        mReader.Read(protocolCapabilities, 0, 8);

        mWriter.Write(protocolCapabilities); // For now we return the same capabilities we read
        mWriter.Flush();

        byte method = mReader.ReadByte();

        // All of this must be tidied up...
        switch (method)
        {
            case ((byte)255):
                ProcessEchoRequest(mReader, mWriter);
                break;


            default:
                // What happens if we invoked a non-supported method...?
                break;
        }

        mWriter.Flush();

        ct.ThrowIfCancellationRequested();
    }

    static void ProcessEchoRequest(BinaryReader reader, BinaryWriter writer)
    {
        string echoRequest = reader.ReadString();
        string reply = $"Reply: {echoRequest}";

        writer.Write(reply);
    }

    BinaryReader? mReader;
    BinaryWriter? mWriter;

    readonly RpcMetrics mServerMetrics;
    readonly RpcSocket mRpcSocket;
    readonly Stopwatch mIdleStopwatch;
    readonly ILogger mLog;
}

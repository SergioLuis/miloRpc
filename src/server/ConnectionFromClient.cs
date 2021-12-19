using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace dotnetRpc.Server;

internal class ConnectionFromClient
{
    internal ConnectionFromClient(RpcSocket socket)
    {
        mRpcSocket = socket;
    }

    internal async ValueTask StartProcessingMessages(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await mRpcSocket.BeginReceiveAsync(ct);
                ProcessMethodCall(ct);
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
    }

    void ProcessMethodCall(CancellationToken ct)
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

    readonly RpcSocket mRpcSocket;
}

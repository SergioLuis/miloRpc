using System.IO;
using System.Net.Sockets;
using dotnetRpc.Shared;

namespace dotnetRpc.Client;

public class ConnectionToServer
{
    public ConnectionToServer(TcpClient tcpClient)
    {
        mTcpClient = tcpClient;
    }

    public string InvokeEchoRequest(string echoRequest) // Temporary code to test changes
    {
        mReader ??= new(new MeteredStream(mTcpClient.GetStream()));
        mWriter ??= new(new MeteredStream(mTcpClient.GetStream()));

        byte protocolVersion = (byte)1;
        byte[] protocolCapabilities = new byte[8];

        mWriter.Write(protocolVersion);
        mWriter.Write(protocolCapabilities, 0, 8);
        mWriter.Flush();

        mReader.Read(protocolCapabilities, 0, 8); // For now we read the capabilities in the same buffer

        mWriter.Write((byte)255); // Method ID for echo request
        mWriter.Write(echoRequest);
        mWriter.Flush();

        // Method executed, now wait for the reply
        string reply = mReader.ReadString();

        return reply;
    }

    BinaryReader? mReader;
    BinaryWriter? mWriter;

    readonly TcpClient mTcpClient;
}

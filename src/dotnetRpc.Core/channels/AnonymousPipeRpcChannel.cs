using System.IO.Pipes;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace dotnetRpc.Core.Channels;

public class AnonymousPipeRpcChannel : IRpcChannel
{
    public MeteredStream Stream { get; }
    public IPEndPoint RemoteEndPoint { get; }

    public AnonymousPipeRpcChannel(
        string connEstablishedFilePath,
        AnonymousPipeServerStream output,
        AnonymousPipeClientStream input)
    {
        mConnectionEstablishedFilePath = connEstablishedFilePath;
        mOutput = output;
        mInput = input;

        RemoteEndPoint = new IPEndPoint(IPAddress.None, -1);
    }

    public void Dispose()
    {
        throw new System.NotImplementedException();
    }

    public async ValueTask WaitForDataAsync(CancellationToken ct)
    {
        throw new System.NotImplementedException();
    }

    public bool IsConnected() => mOutput.IsConnected && mInput.IsConnected;

    readonly string mConnectionEstablishedFilePath;
    readonly AnonymousPipeServerStream mOutput;
    readonly AnonymousPipeClientStream mInput;
}

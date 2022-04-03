using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using dotnetRpc.Core.Shared;

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
        mLog = RpcLoggerFactory.CreateLogger("AnonymousPipeRpcChannel");

        mConnectionEstablishedFilePath = connEstablishedFilePath;
        mOutput = output;
        mInput = input;

        Stream = new MeteredStream(new AnonymousPipeCompositeStream(output, input));

        RemoteEndPoint = new IPEndPoint(IPAddress.None, -1);
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    public async ValueTask WaitForDataAsync(CancellationToken ct)
        => await mInput.ReadAsync(Memory<byte>.Empty, ct);

    public bool IsConnected()
    {
        if (mDisposed)
            return false;

        return mOutput.IsConnected && mInput.IsConnected;
    }

    void Close()
    {
        lock (this)
        {
            if (mDisposed)
                return;

            try
            {
                File.Delete(mConnectionEstablishedFilePath);
            } catch { }

            try
            {
                mOutput.DisposeLocalCopyOfClientHandle();
            } catch { }

            try
            {
                mInput.Dispose();
            } catch { }

            mDisposed = true;
        }
    }

    volatile bool mDisposed = false;
    readonly string mConnectionEstablishedFilePath;
    readonly AnonymousPipeServerStream mOutput;
    readonly AnonymousPipeClientStream mInput;

    readonly ILogger mLog;
}

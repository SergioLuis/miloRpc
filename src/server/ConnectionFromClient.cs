using System;
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
            await mRpcSocket.BeginReceiveAsync(ct);
            ProcessMethodCall();
        }
        catch (Exception ex)
        {
            // TOOD: Handle the exception
            throw;
        }
    }

    void ProcessMethodCall()
    {

    }

    readonly RpcSocket mRpcSocket;
}

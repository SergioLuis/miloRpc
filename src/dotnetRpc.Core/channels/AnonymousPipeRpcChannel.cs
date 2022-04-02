using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace dotnetRpc.Core.Channels;

public class AnonymousPipeRpcChannel : IRpcChannel
{
    public MeteredStream Stream { get; }
    public IPEndPoint RemoteEndPoint { get; }

    public AnonymousPipeRpcChannel()
    {
        
    }

    public void Dispose()
    {
        throw new System.NotImplementedException();
    }

    public async ValueTask WaitForDataAsync(CancellationToken ct)
    {
        throw new System.NotImplementedException();
    }

    public bool IsConnected()
    {
        throw new System.NotImplementedException();
    }
}

using System.Threading;
using System.Threading.Tasks;

using dotnetRpc.Core.Client;
using dotnetRpc.Core.Shared;
using dotnetRpc.Core.Shared.Serialization;

namespace dotnetRpc.Tests;

public interface IVoidCall
{
    Task CallAsync(CancellationToken ct);
}

public class VoidCallProxy : IVoidCall
{
    public VoidCallProxy(ConnectionToServer connection)
    {
        mConnection = connection;
    }

    async Task IVoidCall.CallAsync(CancellationToken ct)
    {
        VoidNetworkMessage req = new();
        VoidNetworkMessage res = new();

        await mConnection.ProcessMethodCallAsync(
            new DefaultMethodId(1),
            new(req, res),
            ct);
    }

    readonly ConnectionToServer mConnection;
}

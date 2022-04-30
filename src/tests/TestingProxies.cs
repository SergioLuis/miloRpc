using System.Threading;
using System.Threading.Tasks;

using miloRPC.Core.Client;
using miloRPC.Core.Shared;
using miloRPC.Serialization;

namespace miloRPC.Tests;

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

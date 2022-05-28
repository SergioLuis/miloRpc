using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

using miloRPC.Core.Client;
using miloRPC.Core.Shared;
using miloRPC.Examples.Shared;
using miloRPC.Serialization;

namespace miloRPC.Examples.Client;

public class EchoServiceProxy : IEchoService
{
    public EchoServiceProxy(ConnectionPool pool)
    {
        mPool = pool;
    }

    async Task<EchoResult> IEchoService.EchoAsync(string message, CancellationToken ct)
    {
        ConnectionToServer conn =
            await mPool.RentConnectionAsync(TimeSpan.FromMilliseconds(200), ct);
        try
        {
            NetworkMessage<string> req = new(message);
            NetworkMessage<EchoResult> res = new();

            await conn.ProcessMethodCallAsync(
                Methods.Echo,
                new RpcNetworkMessages(req, res),
                ct);

            Contract.Assert(res.Val1 is not null);

            return res.Val1;
        }
        finally
        {
            mPool.ReturnConnection(conn);
        }
    }

    readonly ConnectionPool mPool;
}

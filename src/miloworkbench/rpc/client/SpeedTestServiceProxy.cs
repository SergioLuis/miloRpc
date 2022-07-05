using System;
using System.Buffers;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

using miloRPC.Core.Client;
using miloRPC.Core.Shared;
using miloRPC.Serialization;

using miloRpc.TestWorkBench.Rpc.Shared;

namespace miloRpc.TestWorkBench.Rpc.Client;

public class SpeedTestServiceProxy : ISpeedTestService
{
    public SpeedTestServiceProxy(ConnectionPool connectionPool)
    {
        mConnectionPool = connectionPool;
    }
    
    async Task ISpeedTestService.DownloadAsync(int sizeInBytes, CancellationToken ct)
    {
        ConnectionToServer conn = await mConnectionPool.RentConnectionAsync(TimeSpan.Zero, ct);
        byte[] array = ArrayPool<byte>.Shared.Rent(sizeInBytes);
        try
        {
            NetworkMessage<int> req = new(sizeInBytes);
            ByteArrayMessage res = new(array, 0);

            await conn.ProcessMethodCallAsync(
                Methods.SpeedTestService.SpeedTestDownload,
                new RpcNetworkMessages(req, res),
                ct);
            
            Contract.Assert(res.Length == sizeInBytes);
        }
        finally
        {
            mConnectionPool.ReturnConnection(conn);
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    async Task ISpeedTestService.UploadAsync(byte[] buffer, int length, CancellationToken ct)
    {
        ConnectionToServer conn = await mConnectionPool.RentConnectionAsync(TimeSpan.Zero, ct);
        try
        {
            ByteArrayMessage req = new(buffer, length);
            VoidNetworkMessage res = new();

            await conn.ProcessMethodCallAsync(
                Methods.SpeedTestService.SpeedTestUpload,
                new RpcNetworkMessages(req, res),
                ct);
        }
        finally
        {
            mConnectionPool.ReturnConnection(conn);
        }
    }

    readonly ConnectionPool mConnectionPool;
}

using System.IO;
using System.Threading;
using System.Threading.Tasks;

using miloRPC.Core.Client;
using miloRPC.Core.Shared;
using miloRPC.Serialization;
using miloRpc.TestWorkBench.Rpc.Shared;

namespace miloRpc.TestWorkBench.Rpc.Client;

public class FileTransferServiceProxy : IFileTransferService
{
    public FileTransferServiceProxy(ConnectionPool connectionPool)
    {
        mConnectionPool = connectionPool;
    }

    public async Task<Stream> DownloadFileAsync(
        string remoteFilePath, CancellationToken ct)
    {
        ConnectionToServer conn = await mConnectionPool.RentConnectionAsync(ct);

        NetworkMessage<string> req = new();
        req.Val1 = remoteFilePath;

        DestinationStreamMessage res = new(() =>
        {
            mConnectionPool.ReturnConnection(conn);
        });

        await conn.ProcessMethodCallAsync(
            Methods.FileTransferService.DownloadFile,
            new RpcNetworkMessages(req, res),
            ct);

        return res.Stream;
    }

    public async Task UploadFileAsync(
        string remoteFilePath, Stream file, CancellationToken ct)
    {
        ConnectionToServer conn = await mConnectionPool.RentConnectionAsync(ct);

        SourceStreamMessage<string> req = new(file.Dispose);
        req.Val1 = remoteFilePath;
        req.Stream = file;

        VoidNetworkMessage res = new();

        await conn.ProcessMethodCallAsync(
            Methods.FileTransferService.UploadFile,
            new RpcNetworkMessages(req, res),
            ct);
    }

    readonly ConnectionPool mConnectionPool;
}

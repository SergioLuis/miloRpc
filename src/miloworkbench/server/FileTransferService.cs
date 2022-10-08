using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace miloRpc.TestWorkBench.Server;

public class FileTransferService : IFileTransferService
{
    Task<Stream> IFileTransferService.DownloadFileAsync(
        string remoteFilePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Stream result = new FileStream(remoteFilePath, FileMode.Open, FileAccess.Read);
        return Task.FromResult(result);
    }

    async Task IFileTransferService.UploadFileAsync(
        string remoteFilePath, Stream file, CancellationToken ct)
    {
        await using FileStream dstFile =
            new(remoteFilePath, FileMode.CreateNew, FileAccess.Write);

        await file.CopyToAsync(dstFile, ct);
    }
}

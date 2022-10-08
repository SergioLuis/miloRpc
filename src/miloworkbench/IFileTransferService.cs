using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace miloRpc.TestWorkBench;

public interface IFileTransferService
{
    Task<Stream> DownloadFileAsync(string remoteFilePath, CancellationToken ct);
    Task UploadFileAsync(string remoteFilePath, Stream file, CancellationToken ct);
}

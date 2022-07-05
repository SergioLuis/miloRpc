using System.Threading;
using System.Threading.Tasks;

namespace miloRpc.TestWorkBench;

public interface ISpeedTestService
{
    Task DownloadAsync(int sizeInBytes, CancellationToken ct);
    Task UploadAsync(byte[] buffer, int length, CancellationToken ct);
}

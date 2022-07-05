using System;
using System.Diagnostics.Contracts;

namespace miloRpc.TestWorkBench.Server;

internal class SpeedTestService
{
    internal SpeedTestService()
    {
        byte[] buffer = new byte[MaxBlockSize];
        Random.Shared.NextBytes(buffer);
        mSpeedtestBuffer = buffer;
    }

    internal void DownloadAsync(int sizeInBytes, byte[] buffer)
    {
        Contract.Assert(sizeInBytes <= MaxBlockSize);

        Memory<byte> dst = new(buffer, 0, sizeInBytes);
        mSpeedtestBuffer.CopyTo(dst);
    }

    internal void UploadAsync(ReadOnlyMemory<byte> data)
    {
        Contract.Assert(data.Length > 0);
    }

    readonly ReadOnlyMemory<byte> mSpeedtestBuffer;
    internal const int MaxBlockSize = 4 * 1024 * 1024;
}

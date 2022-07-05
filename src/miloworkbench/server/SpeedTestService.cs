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

    internal unsafe void DownloadAsync(int sizeInBytes, byte[] buffer)
    {
        Contract.Assert(sizeInBytes <= MaxBlockSize);
        fixed (byte *pSource = mSpeedtestBuffer)
        fixed (byte* pDestination = buffer)
        {
            for (int i = 0; i < sizeInBytes; i++)
                pDestination[i] = pSource[i];
        }
    }

    internal void UploadAsync(ReadOnlyMemory<byte> data)
    {
        Contract.Assert(data.Length > 0);
    }

    readonly byte[] mSpeedtestBuffer;
    internal const int MaxBlockSize = 4 * 1024 * 1024;
}

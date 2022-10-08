using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

using Spectre.Console;
using Spectre.Console.Cli;

using miloRPC.Core.Client;
using miloRpc.TestWorkBench.Rpc.Client;

namespace miloRpc.TestWorkBench.Client.Commands;

public class SpeedTestCommand : AsyncCommand<SpeedTestCommand.Settings>
{
    public class Settings : BaseSettings
    {
        [CommandOption("--roundtrips <ROUNDTRIPS>")]
        [Description("The number of roundtrips. A higher number of roundtrips yields more stable results when using protocols with congestion control. Default value is 3")]
        public int Roundtrips { get; set; } = 3;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrEmpty(settings.Uri))
            return ValidationResult.Error("'Uri' argument is mandatory");

        if (!Uri.IsWellFormedUriString(settings.Uri, UriKind.Absolute))
            return ValidationResult.Error("'Uri' is not a well formed URI");

        if (settings.TotalSizeMb < 1)
            return ValidationResult.Error("'totalsizemb' must be greater than 0");
        
        if (settings.BlockSizeMb is < 1 or > 4)
            return ValidationResult.Error("'blocksizemb' must be between 1 and 4");

        if (settings.Roundtrips < 1)
            return ValidationResult.Error("'roundtrips' must be greater than 0");

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        Uri serverUri = new(settings.Uri!);
        if (!CheckServerUri.Check(serverUri))
            return 1;

        MiloConnectionPools connectionPools = BuildConnectionPools.Build(settings);

        ConnectionPool connectionPool = connectionPools.Get(serverUri);
        await connectionPool.WarmupPool();

        return await RunRoundtrips(
            serverUri,
            new SpeedTestServiceProxy(connectionPool),
            settings.TotalSizeMb,
            settings.BlockSizeMb,
            settings.Roundtrips);
    }

    static async Task<int> RunRoundtrips(
        Uri serverUri,
        ISpeedTestService speedTest,
        int totalSizeMb,
        int blockSizeMb,
        int roundtrips)
    {
        try
        {
            for (int i = 1; i <= roundtrips; i++)
            {
                Console.WriteLine($"Roundtrip {i} ({serverUri})");
                await RunRoundtrip(speedTest, totalSizeMb, blockSizeMb);
                Console.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"There was an error running speed test roundtrip: {ex.Message}");
            Console.Error.WriteLine($"StackTrace:{Environment.NewLine}{ex.StackTrace}");
            return 1;
        }
    }

    static async Task RunRoundtrip(
        ISpeedTestService speedTest,
        int totalSizeMb,
        int blockSizeMb)
    {
        Stopwatch sw = Stopwatch.StartNew();
        await RunUpload(speedTest, totalSizeMb, blockSizeMb);
        sw.Stop();

        TimeSpan uploadTime = sw.Elapsed;
        
        sw.Restart();
        await RunDownload(speedTest, totalSizeMb, blockSizeMb);
        sw.Stop();

        TimeSpan downloadTime = sw.Elapsed;

        double uploadMbps = (totalSizeMb << 3) / uploadTime.TotalSeconds;
        double downloadMbps = (totalSizeMb << 3) / downloadTime.TotalSeconds;
        
        Console.WriteLine(
            "Upload speed   = {0,6:0.0}Mbps. Time uploading {1}MB   = {2:0}ms.",
            uploadMbps, totalSizeMb, uploadTime.TotalMilliseconds);
        Console.WriteLine(
            "Download speed = {0,6:0.0}Mbps. Time downloading {1}MB = {2:0}ms.",
            downloadMbps, totalSizeMb, downloadTime.TotalMilliseconds);
    }

    static async Task RunUpload(
        ISpeedTestService speedTest, int totalSizeMb, int blockSizeMb)
    {
        int totalSizeBytes = totalSizeMb * 1024 * 1024;
        int blockSizeBytes = blockSizeMb * 1024 * 1024;

        byte[] buffer = ArrayPool<byte>.Shared.Rent(blockSizeBytes);
        try
        {
            Random.Shared.NextBytes(buffer);

            int uploadedBytes = 0;
            while (uploadedBytes < totalSizeBytes)
            {
                int nextBlockSize = totalSizeBytes - uploadedBytes > blockSizeBytes
                    ? blockSizeBytes
                    : totalSizeBytes - uploadedBytes;

                await speedTest.UploadAsync(
                    buffer,
                    nextBlockSize,
                    default);

                uploadedBytes += nextBlockSize;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static async Task RunDownload(
        ISpeedTestService speedTest, int totalSizeMb, int blockSizeMb)
    {
        int totalSizeBytes = totalSizeMb * 1024 * 1024;
        int blockSizeBytes = blockSizeMb * 1024 * 1024;

        int downloadedBytes = 0;
        while (downloadedBytes < totalSizeBytes)
        {
            int nextBlockSize = totalSizeBytes - downloadedBytes > blockSizeBytes
                ? blockSizeBytes
                : totalSizeBytes - downloadedBytes;

            await speedTest.DownloadAsync(
                nextBlockSize,
                default);

            downloadedBytes += nextBlockSize;
        }
    }
}

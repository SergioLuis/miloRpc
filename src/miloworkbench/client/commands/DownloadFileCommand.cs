using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Spectre.Console;
using Spectre.Console.Cli;

using miloRPC.Core.Client;
using miloRpc.TestWorkBench.Rpc.Client;

namespace miloRpc.TestWorkBench.Client.Commands;

public class DownloadFileCommand : AsyncCommand<DownloadFileCommand.Settings>
{
    public class Settings : BaseSettings
    {
        [CommandArgument(1, "[RemoteFilePath]")]
        [Description("The remote file path to download")]
        public string? RemoteFilePath { get; set; }
        
        [CommandArgument(2, "[LocalFilePath]")]
        [Description("The local file path to download to")]
        public string? LocalFilePath { get; set; }
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrEmpty(settings.Uri))
            return ValidationResult.Error("'Uri' argument is mandatory");
        
        if (!Uri.IsWellFormedUriString(settings.Uri, UriKind.Absolute))
            return ValidationResult.Error("'Uri' is not a well formed URI");

        if (string.IsNullOrEmpty(settings.RemoteFilePath))
            return ValidationResult.Error("'RemoteFilePath' argument is mandatory");
        
        if (string.IsNullOrEmpty(settings.LocalFilePath))
            return ValidationResult.Error("'LocalFilePath' argument is mandatory");
        
        if (File.Exists(settings.LocalFilePath))
            return ValidationResult.Error($"'{settings.LocalFilePath}' already exists");
        
        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        Uri serverUri = new(settings.Uri!);
        if (!CheckServerUri.Check(serverUri))
            return 1;

        CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) =>
        {
            cts.Cancel();
            e.Cancel = true;
        };

        MiloConnectionPools connectionPools = BuildConnectionPools.Build(settings);

        ConnectionPool connectionPool = connectionPools.Get(serverUri);
        await connectionPool.WarmupPool();

        return await RunDownloadFile(
            settings.RemoteFilePath!,
            settings.LocalFilePath!,
            new FileTransferServiceProxy(connectionPool),
            cts.Token);
    }

    static async Task<int> RunDownloadFile(
        string remotePath,
        string localPath,
        IFileTransferService service,
        CancellationToken ct)
    {
        byte[] downloadBuffer = ArrayPool<byte>.Shared.Rent(4 * 1024 * 1024);
        try
        {
            await using Stream remoteFile = await service.DownloadFileAsync(remotePath, ct);
            await using FileStream localFile = File.Create(localPath);

            int bytesWritten = 0;
            while (bytesWritten < remoteFile.Length)
            {
                int read = await remoteFile.ReadAsync(downloadBuffer, ct);
                await localFile.WriteAsync(downloadBuffer.AsMemory(0, read), ct);

                bytesWritten += read;
                
                Console.WriteLine("{0}/{1}", bytesWritten, remoteFile.Length);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ex.StackTrace);

            if (File.Exists(localPath))
                TryDeleteMalformedFile(localPath);

            return 1;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(downloadBuffer);
        }
    }

    static void TryDeleteMalformedFile(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch
        {
            // Nothing to do
        }
    }
}

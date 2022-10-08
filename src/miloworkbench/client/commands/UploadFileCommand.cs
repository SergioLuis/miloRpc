using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Spectre.Console;
using Spectre.Console.Cli;

using miloRPC.Core.Client;
using miloRpc.TestWorkBench.Rpc.Client;

namespace miloRpc.TestWorkBench.Client.Commands;

public class UploadFileCommand : AsyncCommand<UploadFileCommand.Settings>
{
    public class Settings : BaseSettings
    {
        [CommandArgument(1, "[LocalFilePath]")]
        [Description("The local file path to upload")]
        public string? LocalFilePath { get; set; }
        
        [CommandArgument(2, "[RemoteFilePath]")]
        [Description("The remote file path to upload to")]
        public string? RemoteFilePath { get; set; }
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
        
        if (!File.Exists(settings.LocalFilePath))
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

        return await RunUploadFile(
            settings.LocalFilePath!,
            settings.RemoteFilePath!,
            new FileTransferServiceProxy(connectionPool),
            cts.Token);
    }

    static async Task<int> RunUploadFile(
        string localPath,
        string remotePath,
        IFileTransferService service,
        CancellationToken ct)
    {
        try
        {
            await using FileStream localFile = new(localPath, FileMode.Open, FileAccess.Read);
            await service.UploadFileAsync(remotePath, localFile, ct);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ex.StackTrace);

            return 1;
        }
    }
}

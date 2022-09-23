using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

using Spectre.Console;
using Spectre.Console.Cli;

namespace miloRpc.TestWorkBench.Client.Commands;

public class DownloadFileCommand : AsyncCommand<DownloadFileCommand.Settings>
{
    public class Settings : BaseSettings
    {
        [CommandArgument(1, "[RemoteFilePath]")]
        [Description("The remote file pathto download")]
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
        throw new System.NotImplementedException();
    }
}

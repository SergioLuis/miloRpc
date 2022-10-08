using System.ComponentModel;

using Spectre.Console.Cli;

namespace miloRpc.TestWorkBench.Client.Commands;

public class BaseSettings : CommandSettings
{
    [CommandArgument(0, "[Uri]")]
    [Description("The URI of the server to perform the speed test against")]
    public string? Uri { get; set; }

    [CommandOption("--totalsizemb <MB>")]
    [Description("The size, in megabytes, to download and upload on each roundtrip. Default value is 128")]
    public int TotalSizeMb { get; set; } = 128;

    [CommandOption("--blocksizemb <MB>")]
    [Description("The block size, in megabytes, that will be downloaded and uploaded on each method call. Default value is 4")]
    public int BlockSizeMb { get; set; } = 4;

    [CommandOption("--buffersize <BYTES>")]
    [Description("Read/write buffer size, in bytes. If zero, then buffering is disabled. Default is '0'")]
    public int BufferSize { get; set; } = 0;
}

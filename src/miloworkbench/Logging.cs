using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;

using miloRPC.Core.Shared;

namespace miloRpc.TestWorkBench;

internal static class Logging
{
    internal static void ConfigureLogging(bool verbose)
    {
        var config = new LoggingConfiguration();

        var fileTarget = new FileTarget();
        fileTarget.Layout = NLogLayout;
        fileTarget.FileName = NLogFile;
        fileTarget.KeepFileOpen = true;
        
        LoggingRule fileLoggingRule = new("*", LogLevel.Trace, fileTarget);
        config.AddTarget("file", fileTarget);
        config.LoggingRules.Add(fileLoggingRule);

        if (verbose)
        {
            var consoleTarget = new ColoredConsoleTarget();
            consoleTarget.Layout = NLogLayout;

            LoggingRule consoleLoggingRule = new("*", LogLevel.Trace, consoleTarget);
            config.AddTarget("console", consoleTarget);
            config.LoggingRules.Add(consoleLoggingRule);
        }

        LogManager.Configuration = config;
        RpcLoggerFactory.RegisterLoggerFactory(new NLogLoggerFactory());
    }
    
    const string NLogFile = "${basedir}/example.log.txt";
    const string NLogLayout = @"${date:format=HH\:mm\:ss.fff} ${logger} - ${message}";
}

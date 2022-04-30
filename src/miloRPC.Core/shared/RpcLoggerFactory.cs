using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace miloRPC.Core.Shared;

public static class RpcLoggerFactory
{
    public static void RegisterLoggerFactory(ILoggerFactory loggerFactory)
        => _loggerFactory = loggerFactory;

    public static ILogger CreateLogger(string categoryName)
        => _loggerFactory.CreateLogger(categoryName);

    static ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
}

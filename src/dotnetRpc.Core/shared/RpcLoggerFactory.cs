using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace dotnetRpc.Core.Shared;

public static class RpcLoggerFactory
{
    public static void RegisterLoggerFactory(ILoggerFactory loggerFactory)
        => mLoggerFactory = loggerFactory;

    public static ILogger CreateLogger(string categoryName)
        => mLoggerFactory.CreateLogger(categoryName);

    static object mSyncLock = new();
    static ILoggerFactory mLoggerFactory = NullLoggerFactory.Instance;
}

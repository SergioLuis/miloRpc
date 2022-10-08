using System;

namespace miloRPC.Core.Shared;

public class RpcException : Exception
{
    public RpcException(string message)
        : base(message) { }

    public RpcException(string message, Exception innerException)
        : base(message, innerException) { }
}

public class StreamNotConsumedRpcException : RpcException
{
    public StreamNotConsumedRpcException(string message)
        : base(message) { }

    public StreamNotConsumedRpcException(string message, Exception innerException)
        : base(message, innerException) { }
}

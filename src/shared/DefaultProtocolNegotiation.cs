using System;

namespace dotnetRpc.Shared;

public static class DefaultProtocolNegotiation
{
    [Flags]
    public enum RpcCapabilities : byte
    {
        None            = 0,
        Ssl             = 1 << 0,
        Compression     = 1 << 1
    }

    public enum Compression : byte
    {
        None            = 0,
        Brotli          = 1,
        GZip            = 2,
        ZLib            = 3
    }
}

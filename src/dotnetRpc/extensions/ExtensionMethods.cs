using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace dotnetRpc.Extensions;

public static class ExtensionMethods
{
    public static CancellationToken CancelLinkedTokenAfter(
        this CancellationToken originalCt, TimeSpan timeout)
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(originalCt);
        cts.CancelAfter(timeout);
        return cts.Token;
    }
}

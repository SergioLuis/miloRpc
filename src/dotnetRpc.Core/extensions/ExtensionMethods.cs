using System;
using System.Threading;

namespace dotnetRpc.Core.Extensions;

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

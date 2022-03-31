using System;
using System.Net.Sockets;
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

    public static void ShutdownAndCloseSafely(this Socket socket)
    {
        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        finally
        {
            socket.Close();
        }
    }
}

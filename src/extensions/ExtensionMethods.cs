using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace dotnetRpc.Extensions;

internal static class ExtensionMethods
{
    internal static async Task ConnectAsync(
        this TcpClient tcpClient,
        IPEndPoint connectTo,
        int timeoutMillis,
        CancellationToken ct)
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMillis);

        try
        {
            await tcpClient.ConnectAsync(connectTo, cts.Token);
        }
        catch (TaskCanceledException ex)
        {
            if (ct.IsCancellationRequested)
                throw;

            throw new TimeoutException(
                $"Connecting to {connectTo} took more than the configured timeout of {timeoutMillis} ms.",
                ex);
        }
    }

    internal static CancellationToken CancelLinkedTokenAfter(
        this CancellationToken originalCt, TimeSpan timeout)
    {
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(originalCt);
        cts.CancelAfter(timeout);
        return cts.Token;
    }
}

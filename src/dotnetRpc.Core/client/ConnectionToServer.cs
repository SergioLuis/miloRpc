using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Client;

public class ConnectionToServer : IDisposable
{
    public enum Status
    {
        Idling,
        NegotiatingProtocol,
        Writing,
        Waiting,
        Reading,
        Exited
    }

    public Status CurrentStatus { get; private set; } = Status.Idling;

    internal ConnectionToServer(
        INegotiateRpcProtocol negotiateProtocol,
        IWriteMethodId writeMethodId,
        IReadMethodCallResult readMethodCallResult,
        RpcMetrics clientMetrics,
        TcpClient tcpClient)
    {
        mNegotiateProtocol = negotiateProtocol;
        mWriteMethodId = writeMethodId;
        mReadMethodCallResult = readMethodCallResult;
        mClientMetrics = clientMetrics;
        mTcpClient = tcpClient;

        mConnectionId = mClientMetrics.ConnectionStart();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async Task ProcessMethodCallAsync(
        IMethodId methodId,
        RpcNetworkMessages messages,
        CancellationToken ct)
    {
        // This Task.Yield allows the client to fire the task and await it
        // at a later point without having to wait for all the synchronous
        // write operations.
        await Task.Yield();

        uint methodCallId = mClientMetrics.MethodCallStart();
        try
        {
            if (mRpc is null)
            {
                CurrentStatus = Status.NegotiatingProtocol;
                mRpc = await mNegotiateProtocol.NegotiateProtocolAsync(
                    mConnectionId,
                    Unsafe.As<IPEndPoint>(mTcpClient.Client.RemoteEndPoint)!,
                    mTcpClient.GetStream());
            }

            CurrentStatus = Status.Writing;
            mWriteMethodId.WriteMethodId(mRpc.Writer, methodId);
            messages.Request.Serialize(mRpc.Writer);

            CurrentStatus = Status.Waiting;
            await mTcpClient.GetStream().ReadAsync(Memory<byte>.Empty, ct);

            CurrentStatus = Status.Reading;
            MethodCallResult result = mReadMethodCallResult.Read(
                mRpc.Reader,
                out bool isResultAvailable,
                out Exception? ex);

            if (isResultAvailable)
                messages.Response.Deserialize(mRpc.Reader);

            if (ex is not null)
                throw ex;

            if (result == MethodCallResult.NotSupported)
            {
                throw new NotSupportedException(
                    $"Method {methodId} is not supported by the server");
            }
        }
        finally
        {
            mClientMetrics.MethodCallEnd();
        }
    }

    RpcProtocolNegotiationResult? mRpc;
    bool mDisposed;
    readonly uint mConnectionId;
    readonly INegotiateRpcProtocol mNegotiateProtocol;
    readonly IWriteMethodId mWriteMethodId;
    readonly IReadMethodCallResult mReadMethodCallResult;
    readonly RpcMetrics mClientMetrics;
    readonly TcpClient mTcpClient;

    protected virtual void Dispose(bool disposing)
    {
        if (mDisposed)
            return;

        if (disposing)
        {
            mClientMetrics.ConnectionEnd();
            mTcpClient.Dispose();
        }

        mDisposed = true;
    }
}

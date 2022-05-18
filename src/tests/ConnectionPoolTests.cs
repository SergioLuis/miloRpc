using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

using miloRPC.Core.Channels;
using miloRPC.Core.Client;
using miloRPC.Core.Shared;

namespace miloRPC.Tests;

[TestFixture]
public class ConnectionPoolTests
{
    [TestCase(2)]
    [TestCase(4)]
    [TestCase(6)]
    public async Task Warming_Up_Pool_Creates_Minimum_Pooled_Connections(int initialCount)
    {
        RpcMetrics metrics = new();

        Mock<IConnectToServer> connectToServerMock = new(MockBehavior.Strict);
        connectToServerMock.Setup(
            mock => mock.ConnectAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildConnectionToServer(metrics));

        ConnectionPool pool = new(connectToServerMock.Object, initialCount);

        await pool.WarmupPool();

        connectToServerMock.Verify(
            mock => mock.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(initialCount));
    }

    static ConnectionToServer BuildConnectionToServer(RpcMetrics metrics)
        => new(
            DefaultClientProtocolNegotiation.Instance,
            DefaultWriteMethodId.Instance,
            DefaultReadMethodCallResult.Instance,
            metrics,
            new TestRpcChannel());
}

class TestRpcChannel : IRpcChannel
{
    MeteredStream IRpcChannel.Stream => new(System.IO.Stream.Null);
    IPEndPoint IRpcChannel.RemoteEndPoint => new(IPAddress.Loopback, 0);

    async ValueTask IRpcChannel.WaitForDataAsync(CancellationToken ct)
    {
        await Task.Yield();
    }

    bool IRpcChannel.IsConnected() => mIsConnected;

    public void Dispose() => mIsConnected = false;

    bool mIsConnected = true;
}

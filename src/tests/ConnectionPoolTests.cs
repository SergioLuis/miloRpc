using System;
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
            .ReturnsAsync(() => BuildConnectionToServer(metrics));

        ConnectionPool pool = new(connectToServerMock.Object, initialCount);

        await pool.WarmupPool();

        connectToServerMock.Verify(
            mock => mock.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(initialCount));

        Assert.That(pool.PooledConnections, Is.EqualTo(initialCount));
        Assert.That(pool.RentedConnections, Is.EqualTo(0));
        Assert.That(pool.WaitingThreads, Is.EqualTo(0));
    }

    [Test]
    public async Task Returned_Connections_Are_Rented_Again()
    {
        RpcMetrics metrics = new();

        Mock<IConnectToServer> connectToServerMock = new(MockBehavior.Strict);
        connectToServerMock.Setup(
                mock => mock.ConnectAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => BuildConnectionToServer(metrics));

        ConnectionPool pool = new(connectToServerMock.Object, 2);

        await pool.WarmupPool();

        connectToServerMock.Verify(
            mock => mock.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        Task<ConnectionToServer> firstRentTask = pool.RentConnectionAsync();
        Task<ConnectionToServer> secondRentTask = pool.RentConnectionAsync();

        Assert.That(() => firstRentTask.IsCompleted, Is.True.After(1000, 100));
        Assert.That(() => secondRentTask.IsCompleted, Is.True.After(1000, 100));

        Task<ConnectionToServer> thirdRentTask = pool.RentConnectionAsync();

        Assert.That(() => thirdRentTask.IsCompleted, Is.False.After(1000, 100));

        ConnectionToServer _ = await firstRentTask;
        ConnectionToServer secondConnection = await secondRentTask;

        Assert.That(pool.RentedConnections, Is.EqualTo(2));
        Assert.That(pool.WaitingThreads, Is.EqualTo(1));

        pool.ReturnConnection(secondConnection);

        Assert.That(() => thirdRentTask.IsCompleted, Is.True.After(1000, 100));

        ConnectionToServer secondConnectionRentedAgain = await thirdRentTask;

        Assert.That(ReferenceEquals(secondConnection, secondConnectionRentedAgain));

        Assert.That(pool.PooledConnections, Is.EqualTo(0));
        Assert.That(pool.RentedConnections, Is.EqualTo(2));
        Assert.That(pool.WaitingThreads, Is.EqualTo(0));
    }

    [Test]
    public async Task Disconnected_Connections_Are_Not_Returned_To_Pool()
    {
        RpcMetrics metrics = new();

        Mock<IConnectToServer> connectToServerMock = new(MockBehavior.Strict);
        connectToServerMock.Setup(
                mock => mock.ConnectAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => BuildConnectionToServer(metrics));

        ConnectionPool pool = new(connectToServerMock.Object, 2);

        await pool.WarmupPool();

        connectToServerMock.Verify(
            mock => mock.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        Task<ConnectionToServer> firstRentTask = pool.RentConnectionAsync();
        Task<ConnectionToServer> secondRentTask = pool.RentConnectionAsync();

        Assert.That(() => firstRentTask.IsCompleted, Is.True.After(1000, 100));
        Assert.That(() => secondRentTask.IsCompleted, Is.True.After(1000, 100));

        Task<ConnectionToServer> thirdRentTask = pool.RentConnectionAsync();

        Assert.That(() => thirdRentTask.IsCompleted, Is.False.After(1000, 100));

        ConnectionToServer firstConnection = await firstRentTask;
        ConnectionToServer secondConnection = await secondRentTask;

        Assert.That(pool.RentedConnections, Is.EqualTo(2));
        Assert.That(pool.WaitingThreads, Is.EqualTo(1));

        firstConnection.Dispose();
        pool.ReturnConnection(firstConnection);

        Assert.That(pool.RentedConnections, Is.EqualTo(1));
        Assert.That(pool.WaitingThreads, Is.EqualTo(1));
        Assert.That(() => thirdRentTask.IsCompleted, Is.False.After(1000, 100));

        pool.ReturnConnection(secondConnection);

        Assert.That(() => thirdRentTask.IsCompleted, Is.True.After(1000, 100));

        ConnectionToServer secondConnectionRentedAgain = await thirdRentTask;

        Assert.That(ReferenceEquals(secondConnection, secondConnectionRentedAgain));

        Assert.That(pool.PooledConnections, Is.EqualTo(0));
        Assert.That(pool.RentedConnections, Is.EqualTo(1));
        Assert.That(pool.WaitingThreads, Is.EqualTo(0));
    }

    [Test]
    public async Task Disconnected_Connections_Are_Not_Returned_From_Pool()
    {
        RpcMetrics metrics = new();

        Mock<IConnectToServer> connectToServerMock = new(MockBehavior.Strict);
        connectToServerMock.Setup(
                mock => mock.ConnectAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => BuildConnectionToServer(metrics));

        ConnectionPool pool = new(connectToServerMock.Object, 2);

        await pool.WarmupPool();

        connectToServerMock.Verify(
            mock => mock.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        Task<ConnectionToServer> firstRentTask = pool.RentConnectionAsync();
        Task<ConnectionToServer> secondRentTask = pool.RentConnectionAsync();

        Assert.That(() => firstRentTask.IsCompleted, Is.True.After(1000, 100));
        Assert.That(() => secondRentTask.IsCompleted, Is.True.After(1000, 100));

        ConnectionToServer firstConnection = await firstRentTask;
        ConnectionToServer secondConnection = await secondRentTask;

        pool.ReturnConnection(firstConnection);
        pool.ReturnConnection(secondConnection);

        firstConnection.Dispose();
        secondConnection.Dispose();

        Task<ConnectionToServer> thirdRentTask = pool.RentConnectionAsync();
        Assert.That(() => thirdRentTask.IsCompleted, Is.True.After(1000, 100));

        Assert.That(pool.RentedConnections, Is.EqualTo(1));
        // The pool had to create new connections to satisfy the third request
        // Because there were no waiting threads, it created:
        //   2 (minimum) + 1 (for the request being served)
        Assert.That(pool.PooledConnections, Is.EqualTo(2));

        connectToServerMock.Verify(
            mock => mock.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(5));

        ConnectionToServer thirdConnection = await thirdRentTask;

        Assert.That(ReferenceEquals(firstConnection, thirdConnection), Is.False);
        Assert.That(ReferenceEquals(secondConnection, thirdConnection), Is.False);

        Assert.That(pool.WaitingThreads, Is.EqualTo(0));
    }

    [Test]
    public async Task Connections_Are_Created_Immediately_If_Wait_Time_Is_Zero()
    {
        RpcMetrics metrics = new();

        Mock<IConnectToServer> connectToServerMock = new(MockBehavior.Strict);
        connectToServerMock.Setup(
                mock => mock.ConnectAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => BuildConnectionToServer(metrics));

        ConnectionPool pool = new(connectToServerMock.Object, 2);

        await pool.WarmupPool();

        connectToServerMock.Verify(
            mock => mock.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        Task<ConnectionToServer> firstRentTask = pool.RentConnectionAsync();
        Task<ConnectionToServer> secondRentTask = pool.RentConnectionAsync();

        Assert.That(() => firstRentTask.IsCompleted, Is.True.After(1000, 100));
        Assert.That(() => secondRentTask.IsCompleted, Is.True.After(1000, 100));

        Assert.That(pool.PooledConnections, Is.EqualTo(0));
        Assert.That(pool.RentedConnections, Is.EqualTo(2));

        Task<ConnectionToServer> thirdRentTask = pool.RentConnectionAsync(TimeSpan.Zero);
        Assert.That(() => thirdRentTask.IsCompleted, Is.True.After(1000, 100));

        Assert.That(pool.RentedConnections, Is.EqualTo(3));
        // The pool had to create new connections to satisfy the third request
        // Because there were no waiting threads, it created:
        //   2 (minimum) + 1 (for the request being served)
        Assert.That(pool.PooledConnections, Is.EqualTo(2));
        Assert.That(pool.WaitingThreads, Is.EqualTo(0));

        connectToServerMock.Verify(
            mock => mock.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(5));
    }

    [TestCase(0)]
    [TestCase(200)]
    public async Task Pool_Creates_Enough_Connections_To_Satisfy_All_Waiting_Threads(
        int unblockingConnectionWaitTime)
    {
        RpcMetrics metrics = new();

        Mock<IConnectToServer> connectToServerMock = new(MockBehavior.Strict);
        connectToServerMock.Setup(
                mock => mock.ConnectAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => BuildConnectionToServer(metrics));

        ConnectionPool pool = new(connectToServerMock.Object, 1);

        await pool.WarmupPool();

        connectToServerMock.Verify(
            mock => mock.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(1));

        ConnectionToServer _ = await pool.RentConnectionAsync();

        Task<ConnectionToServer> firstBlockedTask = pool.RentConnectionAsync();
        Task<ConnectionToServer> secondBlockedTask = pool.RentConnectionAsync();
        Task<ConnectionToServer> thirdBlockedTask = pool.RentConnectionAsync();
        Task<ConnectionToServer> fourthBlockedTask = pool.RentConnectionAsync();
        Task<ConnectionToServer> fifthBlockedTask = pool.RentConnectionAsync();

        Assert.That(pool.RentedConnections, Is.EqualTo(1));
        Assert.That(() => pool.WaitingThreads, Is.EqualTo(5).After(1000, 100));

        ConnectionToServer unblockingConnection =
            await pool.RentConnectionAsync(
                TimeSpan.FromMilliseconds(unblockingConnectionWaitTime));

        connectToServerMock.Verify(
            mock => mock.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(
                1       // First warmup
                + 5     // One for each request waiting
                + 1     // One to satisfy the request that triggered connection creation
                + 1));  // Minimum number of pooled connections

        Assert.That(() => pool.RentedConnections, Is.EqualTo(7).After(1000, 100));
        Assert.That(pool.WaitingThreads, Is.EqualTo(0));

        Assert.That(firstBlockedTask.IsCompleted, Is.True);
        Assert.That(secondBlockedTask.IsCompleted, Is.True);
        Assert.That(thirdBlockedTask.IsCompleted, Is.True);
        Assert.That(fourthBlockedTask.IsCompleted, Is.True);
        Assert.That(fifthBlockedTask.IsCompleted, Is.True);

        Assert.That(pool.WaitingThreads, Is.EqualTo(0));
    }

    [Test, Timeout(2000)]
    public async Task Client_Does_Not_Starve_If_Pool_Cannot_Satisfy_Request()
    {
        RpcMetrics metrics = new();

        Mock<IConnectToServer> connectToServerMock = new(MockBehavior.Strict);
        connectToServerMock.Setup(
                mock => mock.ConnectAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => BuildConnectionToServer(metrics));

        ConnectionPool pool = new(connectToServerMock.Object, 0);

        ConnectionToServer _ = await pool.RentConnectionAsync();

        Assert.That(pool.PooledConnections, Is.EqualTo(0));
        Assert.That(pool.RentedConnections, Is.EqualTo(1));
        Assert.That(pool.WaitingThreads, Is.EqualTo(0));
    }

    [Test]
    public async Task Pool_Disposes_Unknown_Returned_Connections()
    {
        RpcMetrics metrics = new();

        Mock<IConnectToServer> connectToServerMock = new(MockBehavior.Strict);
        connectToServerMock.Setup(
                mock => mock.ConnectAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => BuildConnectionToServer(metrics));

        ConnectionToServer unknownConn =
            await connectToServerMock.Object.ConnectAsync(CancellationToken.None);

        ConnectionPool pool = new(connectToServerMock.Object, 0);

        Assert.That(unknownConn.IsConnected(), Is.True);

        pool.ReturnConnection(unknownConn);

        Assert.That(unknownConn.IsConnected(), Is.False);

        Assert.That(pool.PooledConnections, Is.EqualTo(0));
        Assert.That(pool.RentedConnections, Is.EqualTo(0));
        Assert.That(pool.WaitingThreads, Is.EqualTo(0));
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
    string IRpcChannel.ChannelProtocol => "test";
    MeteredStream IRpcChannel.Stream => new(System.IO.Stream.Null);
    IPEndPoint IRpcChannel.LocalEndPoint => new(IPAddress.Loopback, 9999);
    IPEndPoint IRpcChannel.RemoteEndPoint => new(IPAddress.Loopback, 0);

    async ValueTask IRpcChannel.WaitForDataAsync(CancellationToken ct)
    {
        await Task.Yield();
    }

    bool IRpcChannel.IsConnected() => mIsConnected;

    public void Dispose() => mIsConnected = false;

    bool mIsConnected = true;
}

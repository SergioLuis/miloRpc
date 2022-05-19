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

        connectToServerMock.Verify(
            mock => mock.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(5));
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

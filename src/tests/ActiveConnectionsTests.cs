using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using dotnetRpc.Core.Client;
using dotnetRpc.Core.Server;

namespace dotnetRpc.Tests;

[TestFixture]
public class ActiveConnectionsTests
{
    [Test, Timeout(TestingConstants.Timeout)]
    public async Task Active_Connections_Can_Be_Closed_From_The_Client()
    {
        CancellationTokenSource cts = new();
        cts.CancelAfter(TestingConstants.Timeout);

        IPEndPoint endpoint = new(IPAddress.Loopback, port: 0);

        StubCollection stubCollection = new();
        IServer tcpServer = new TcpServer(endpoint, stubCollection);
        Task serverTask = tcpServer.ListenAsync(cts.Token);

        Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

        ConnectToServer connectToServer = new(tcpServer.BindAddress!);

        Assert.That(tcpServer.ActiveConnections.Connections, Is.Empty);

        ConnectionToServer firstConn = await connectToServer.ConnectAsync(cts.Token);
        ConnectionToServer secondConn = await connectToServer.ConnectAsync(cts.Token);
        ConnectionToServer thirdConn = await connectToServer.ConnectAsync(cts.Token);

        Assert.That(
            () => tcpServer.ActiveConnections.Connections,
            Has.Count.EqualTo(3).After(3000, 10));

        ActiveConnections.ActiveConnection? firstConnFromClient =
            tcpServer.ActiveConnections.Connections.FirstOrDefault(
                activeConn => activeConn.Conn.ConnectionId == 1);

        Assert.That(firstConnFromClient, Is.Not.Null);

        ActiveConnections.ActiveConnection? secondConnFromClient =
            tcpServer.ActiveConnections.Connections.FirstOrDefault(
                activeConn => activeConn.Conn.ConnectionId == 2);

        Assert.That(secondConnFromClient, Is.Not.Null);

        ActiveConnections.ActiveConnection? thirdConnFromClient =
            tcpServer.ActiveConnections.Connections.FirstOrDefault(
                activeConn => activeConn.Conn.ConnectionId == 3);

        Assert.That(thirdConnFromClient, Is.Not.Null);

        Assert.That(firstConn.IsConnected(), Is.True);
        Assert.That(firstConnFromClient!.Conn.IsConnected(), Is.True);

        Assert.That(secondConn.IsConnected(), Is.True);
        Assert.That(secondConnFromClient!.Conn.IsConnected(), Is.True);

        Assert.That(thirdConn.IsConnected(), Is.True);
        Assert.That(thirdConnFromClient!.Conn.IsConnected(), Is.True);

        firstConn.Dispose();
        secondConn.Dispose();
        thirdConn.Dispose();

        Assert.That(
            () => firstConnFromClient.Conn.CurrentStatus,
            Is.EqualTo(ConnectionFromClient.Status.Exited).After(1000, 10));

        Assert.That(
            () => secondConnFromClient.Conn.CurrentStatus,
            Is.EqualTo(ConnectionFromClient.Status.Exited).After(1000, 10));

        Assert.That(
            () => thirdConnFromClient.Conn.CurrentStatus,
            Is.EqualTo(ConnectionFromClient.Status.Exited).After(1000, 10));

        // The ActiveConnections monitor loop gets triggered after 30 seconds
        // unless a recollection is forced
        Assert.That(
            () => tcpServer.ActiveConnections.Connections,
            Has.Count.EqualTo(3).After(1000, 10));

        await tcpServer.ActiveConnections.ForceConnectionRecollectAsync();

        Assert.That(
            () => tcpServer.ActiveConnections.Connections,
            Has.Count.EqualTo(0).After(1000, 10));

        Assert.That(firstConn.IsConnected(), Is.False);
        Assert.That(firstConnFromClient.Conn.IsConnected(), Is.False);

        Assert.That(secondConn.IsConnected(), Is.False);
        Assert.That(secondConnFromClient.Conn.IsConnected(), Is.False);

        Assert.That(thirdConn.IsConnected(), Is.False);
        Assert.That(thirdConnFromClient.Conn.IsConnected(), Is.False);
    }

    [Test, Timeout(TestingConstants.Timeout)]
    public async Task Active_Connections_Can_Be_Closed_From_The_Server()
    {
        CancellationTokenSource cts = new();
        cts.CancelAfter(TestingConstants.Timeout);

        IPEndPoint endpoint = new(IPAddress.Loopback, port: 0);

        StubCollection stubCollection = new();
        IServer tcpServer = new TcpServer(endpoint, stubCollection);
        Task serverTask = tcpServer.ListenAsync(cts.Token);

        Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

        ConnectToServer connectToServer = new(tcpServer.BindAddress!);

        Assert.That(tcpServer.ActiveConnections.Connections, Is.Empty);

        ConnectionToServer firstConn = await connectToServer.ConnectAsync(cts.Token);
        ConnectionToServer secondConn = await connectToServer.ConnectAsync(cts.Token);
        ConnectionToServer thirdConn = await connectToServer.ConnectAsync(cts.Token);

        Assert.That(
            () => tcpServer.ActiveConnections.Connections,
            Has.Count.EqualTo(3).After(3000, 10));

        ActiveConnections.ActiveConnection? firstConnFromClient =
            tcpServer.ActiveConnections.Connections.FirstOrDefault(
                activeConn => activeConn.Conn.ConnectionId == 1);

        Assert.That(firstConnFromClient, Is.Not.Null);

        ActiveConnections.ActiveConnection? secondConnFromClient =
            tcpServer.ActiveConnections.Connections.FirstOrDefault(
                activeConn => activeConn.Conn.ConnectionId == 2);

        Assert.That(secondConnFromClient, Is.Not.Null);

        ActiveConnections.ActiveConnection? thirdConnFromClient =
            tcpServer.ActiveConnections.Connections.FirstOrDefault(
                activeConn => activeConn.Conn.ConnectionId == 3);

        Assert.That(thirdConnFromClient, Is.Not.Null);

        Assert.That(firstConn.IsConnected(), Is.True);
        Assert.That(firstConnFromClient!.Conn.IsConnected(), Is.True);

        Assert.That(secondConn.IsConnected(), Is.True);
        Assert.That(secondConnFromClient!.Conn.IsConnected(), Is.True);

        Assert.That(thirdConn.IsConnected(), Is.True);
        Assert.That(thirdConnFromClient!.Conn.IsConnected(), Is.True);

        firstConnFromClient!.Cts.Cancel();
        secondConnFromClient!.Cts.Cancel();
        thirdConnFromClient!.Cts.Cancel();

        Assert.That(
            () => firstConnFromClient.Conn.CurrentStatus,
            Is.EqualTo(ConnectionFromClient.Status.Exited).After(1000, 10));

        Assert.That(
            () => secondConnFromClient.Conn.CurrentStatus,
            Is.EqualTo(ConnectionFromClient.Status.Exited).After(1000, 10));

        Assert.That(
            () => thirdConnFromClient.Conn.CurrentStatus,
            Is.EqualTo(ConnectionFromClient.Status.Exited).After(1000, 10));

        // The ActiveConnections monitor loop gets triggered after 30 seconds
        // unless a recollection is forced
        Assert.That(
            () => tcpServer.ActiveConnections.Connections,
            Has.Count.EqualTo(3).After(1000, 10));

        await tcpServer.ActiveConnections.ForceConnectionRecollectAsync();

        Assert.That(
            () => tcpServer.ActiveConnections.Connections,
            Has.Count.EqualTo(0).After(1000, 10));

        Assert.That(firstConn.IsConnected(), Is.False);
        Assert.That(firstConnFromClient.Conn.IsConnected(), Is.False);

        Assert.That(secondConn.IsConnected(), Is.False);
        Assert.That(secondConnFromClient.Conn.IsConnected(), Is.False);

        Assert.That(thirdConn.IsConnected(), Is.False);
        Assert.That(thirdConnFromClient.Conn.IsConnected(), Is.False);
    }

    [Test, Timeout(TestingConstants.Timeout)]
    public async Task Connection_Gets_Closed_After_Idling_Timeout()
    {
        const int IdlingTimeoutMs = 2000;

        CancellationTokenSource cts = new();
        cts.CancelAfter(TestingConstants.Timeout);

        IPEndPoint endpoint = new(IPAddress.Loopback, port: 0);

        StubCollection stubCollection = new();
        IServer tcpServer = new TcpServer(endpoint, stubCollection);
        tcpServer.ConnectionTimeouts.Idling = TimeSpan.FromMilliseconds(IdlingTimeoutMs);
        Task serverTask = tcpServer.ListenAsync(cts.Token);

        Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

        ConnectToServer connectToServer = new(tcpServer.BindAddress!);

        Assert.That(tcpServer.ActiveConnections.Connections, Is.Empty);

        ConnectionToServer conn = await connectToServer.ConnectAsync(cts.Token);

        Assert.That(
            () => tcpServer.ActiveConnections.Connections,
            Has.Count.EqualTo(1).After(3000, 10));

        ActiveConnections.ActiveConnection? connFromClient =
            tcpServer.ActiveConnections.Connections.FirstOrDefault(
                activeConn => activeConn.Conn.ConnectionId == 1);

        Assert.That(connFromClient, Is.Not.Null);

        Assert.That(conn.IsConnected(), Is.True);
        Assert.That(connFromClient!.Conn.IsConnected(), Is.True);

        Assert.That(
            conn.CurrentStatus,
            Is.EqualTo(ConnectionToServer.Status.Idling));
        Assert.That(
            connFromClient.Conn.CurrentStatus,
            Is.EqualTo(ConnectionFromClient.Status.Idling));

        // Now we wait for the Idling timeout to kick-in...

        Assert.That(
            () => conn.CurrentStatus,
            Is.EqualTo(ConnectionToServer.Status.Exited).After(IdlingTimeoutMs + 1000, 10));
        Assert.That(
            () => connFromClient.Conn.CurrentStatus,
            Is.EqualTo(ConnectionFromClient.Status.Exited).After(IdlingTimeoutMs + 1000, 10));

        // The ActiveConnections monitor loop gets triggered after 30 seconds
        // unless a recollection is forced
        Assert.That(
            () => tcpServer.ActiveConnections.Connections,
            Has.Count.EqualTo(1).After(1000, 10));

        await tcpServer.ActiveConnections.ForceConnectionRecollectAsync();

        Assert.That(
            () => tcpServer.ActiveConnections.Connections,
            Has.Count.EqualTo(0).After(1000, 10));

        Assert.That(conn.IsConnected(), Is.False);
        Assert.That(connFromClient.Conn.IsConnected(), Is.False);

        Assert.That(
            conn.CurrentStatus,
            Is.EqualTo(ConnectionToServer.Status.Exited));
        Assert.That(
            connFromClient.Conn.CurrentStatus,
            Is.EqualTo(ConnectionFromClient.Status.Exited));
    }
}

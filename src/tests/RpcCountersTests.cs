using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using miloRPC.Core.Client;
using miloRPC.Core.Server;
using miloRPC.Channels.Tcp;

namespace miloRPC.Tests;

[TestFixture]
public class RpcCountersTests
{
    [Test, Timeout(TestingConstants.Timeout)]
    public async Task TcpServer_Connections_Increases_And_Decreases()
    {
        CancellationTokenSource cts = new();
        cts.CancelAfter(TestingConstants.Timeout);

        IPEndPoint endpoint = new(IPAddress.Loopback, port: 0);

        StubCollection stubCollection = new(new VoidCallStub());
        IServer tcpServer = new TcpServer(endpoint, stubCollection);
        Task serverTask = tcpServer.ListenAsync(cts.Token);

        Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

        ConnectToTcpServer connectToTcpServer = new(tcpServer.BindAddress!);

        Assert.That(
            () => tcpServer.ActiveConnections.Counters.TotalConnections,
            Is.Zero.After(100, 10));
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveConnections,
            Is.Zero.After(100, 10));

        ConnectionToServer firstConnection = await connectToTcpServer.ConnectAsync(cts.Token);
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.TotalConnections,
            Is.EqualTo(1).After(100, 10));
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveConnections,
            Is.EqualTo(1).After(1000, 10));

        ConnectionToServer secondConnection = await connectToTcpServer.ConnectAsync(cts.Token);
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.TotalConnections,
            Is.EqualTo(2).After(1000, 10));
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveConnections,
            Is.EqualTo(2).After(1000, 10));

        firstConnection.Dispose();
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.TotalConnections,
            Is.EqualTo(2).After(1000, 10));
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveConnections,
            Is.EqualTo(1).After(1000, 10));

        secondConnection.Dispose();
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.TotalConnections,
            Is.EqualTo(2).After(1000, 10));
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveConnections,
            Is.EqualTo(0).After(1000, 10));

        cts.Cancel();

        await serverTask;
    }

    [Test, Timeout(TestingConstants.Timeout)]
    public async Task TcpServer_MethodCalls_Increases_And_Decreases()
    {
        CancellationTokenSource cts = new();
        cts.CancelAfter(TestingConstants.Timeout);

        IPEndPoint endpoint = new(IPAddress.Loopback, port: 0);

        LongRunningVoidCallStub voidCallStub = new();
        StubCollection stubCollection = new(voidCallStub);
        IServer tcpServer = new TcpServer(endpoint, stubCollection);
        Task serverTask = tcpServer.ListenAsync(cts.Token);

        Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

        ConnectToTcpServer connectToTcpServer = new(tcpServer.BindAddress!);

        Assert.That(
            () => tcpServer.ActiveConnections.Counters.TotalMethodCalls,
            Is.Zero.After(100, 10));
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveMethodCalls,
            Is.Zero.After(100, 10));

        Task<ConnectionToServer> firstConnTask = connectToTcpServer.ConnectAsync(cts.Token);
        Task<ConnectionToServer> secondConnTask = connectToTcpServer.ConnectAsync(cts.Token);

        await Task.WhenAll(firstConnTask, secondConnTask);

        ConnectionToServer firstConnection = await firstConnTask;
        IVoidCall firstProxy = new VoidCallProxy(firstConnection);

        ConnectionToServer secondConnection = await secondConnTask;
        IVoidCall secondProxy = new VoidCallProxy(secondConnection);

        voidCallStub.Set();

        Task firstMethodCallTask = firstProxy.CallAsync(cts.Token);
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.TotalMethodCalls,
            Is.EqualTo(1).After(100, 10));
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveMethodCalls,
            Is.EqualTo(1).After(100, 10));

        Task secondMethodCallTask = secondProxy.CallAsync(cts.Token);
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.TotalMethodCalls,
            Is.EqualTo(2).After(100, 10));
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveMethodCalls,
            Is.EqualTo(2).After(100, 10));

        voidCallStub.Reset();

        await Task.WhenAll(firstMethodCallTask, secondMethodCallTask);

        Assert.That(
            () => tcpServer.ActiveConnections.Counters.TotalMethodCalls,
            Is.EqualTo(2).After(100, 10));
        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveMethodCalls,
            Is.EqualTo(0).After(100, 10));

        firstConnection.Dispose();
        secondConnection.Dispose();

        cts.Cancel();

        await serverTask;
    }
}

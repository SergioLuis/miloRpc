using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using miloRPC.Channels.Tcp;
using miloRPC.Core.Client;
using miloRPC.Core.Server;
using miloRPC.Core.Shared;
using miloRPC.Serialization;

namespace miloRPC.Tests;

[TestFixture]
public class EndOfDataSequenceTests
{
    [Test, Timeout(TestingConstants.Timeout)]
    public async Task Server_Is_Able_To_Consume_Until_End_Of_Data()
    {
        CancellationTokenSource cts = new();
        cts.CancelAfter(TestingConstants.Timeout);

        IPEndPoint endpoint = new(IPAddress.Loopback, port: 0);

        StubCollection stubCollection = new(new VoidCallStub());
        IServer<IPEndPoint> tcpServer = new TcpServer(endpoint, stubCollection);
        Task serverTask = tcpServer.ListenAsync(cts.Token);
        
        Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 100));

        IConnectToServer connectToServer = new ConnectToTcpServer(tcpServer.BindAddress!);

        Assert.That(tcpServer.Connections.All, Is.Empty);

        ConnectionToServer conn = await connectToServer.ConnectAsync(cts.Token);
        
        Assert.That(
            () => tcpServer.Connections.All,
            Has.Count.EqualTo(1).After(1).Seconds.PollEvery(100).MilliSeconds);

        IVoidCall voidCallProxy = new VoidCallProxy(conn);

        Random rand = new(Environment.TickCount);
        byte[] callData = new byte[256];
        rand.NextBytes(callData);

        DefaultMethodId unsupportedMethodId = new(255, "Unsupported");
        NetworkMessage<Memory<byte>> request = new(callData);
        NetworkMessage<bool> response = new();

        Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await conn.ProcessMethodCallAsync(
                unsupportedMethodId,
                new RpcNetworkMessages(request, response),
                cts.Token);
        });
        
        Assert.That(
            () => conn.IsConnected(),
            Is.True.After(1).Seconds.PollEvery(100).MilliSeconds);

        await voidCallProxy.CallAsync(cts.Token);

        conn.Dispose();
        cts.Cancel();
        await serverTask;
    }
}

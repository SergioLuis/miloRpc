using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

using dotnetRpc.Core.Client;
using dotnetRpc.Core.Server;
using dotnetRpc.Core.Shared;
using dotnetRpc.Core.Shared.Serialization;

namespace dotnetRpc.Tests;

[TestFixture]
public class TcpEndToEndTests
{
    [Test, Timeout(TestingConstants.Timeout), TestCaseSource(nameof(RpcCapabilitiesCombinations))]
    public async Task Remote_Procedure_Call_Works_Ok_End_To_End(RpcCapabilities capabilities)
    {
        int acceptLoopStartEvents = 0;
        EventHandler<AcceptLoopStartEventArgs> acceptLoopStartEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            Assert.That(args.LaunchCount, Is.EqualTo(acceptLoopStartEvents));
            Assert.That(args.CancelRequested, Is.False);
            acceptLoopStartEvents++;
        };

        int acceptLoopStopEvents = 0;
        EventHandler<AcceptLoopStopEventArgs> acceptLoopStopEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            acceptLoopStopEvents++;
            Assert.That(args.LaunchCount, Is.EqualTo(acceptLoopStopEvents));
        };

        int connAcceptEvents = 0;
        EventHandler<ConnectionAcceptEventArgs> connAcceptEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            connAcceptEvents++;
            Assert.That(args.CancelRequested, Is.False);
        };

        Mock<IServerFunctionality> serverFuncMock = new(MockBehavior.Strict);
        serverFuncMock.Setup(
                mock => mock.CallAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        CancellationTokenSource cts = new();
        cts.CancelAfter(TestingConstants.Timeout);

        INegotiateRpcProtocol negotiateServerProtocol =
            new DefaultServerProtocolNegotiation(
                capabilities,
                capabilities,
                ArrayPool<byte>.Shared,
                mCertificatePath,
                "c3rt1f1c4t3p4ssw0rd");

        INegotiateRpcProtocol negotiateClientProtocol =
            new DefaultClientProtocolNegotiation(
                capabilities,
                RpcCapabilities.None,
                ArrayPool<byte>.Shared,
                DefaultClientProtocolNegotiation.AcceptAllCertificates);

        IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);

        StubCollection stubCollection = new(new ServerFunctionalityStub(serverFuncMock.Object));
        IServer tcpServer = new TcpServer(endPoint, stubCollection, negotiateServerProtocol);
        tcpServer.AcceptLoopStart += acceptLoopStartEventHandler;
        tcpServer.AcceptLoopStop += acceptLoopStopEventHandler;
        tcpServer.ConnectionAccept += connAcceptEventHandler;

        Task serverTask = tcpServer.ListenAsync(cts.Token);

        Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

        ConnectToServer connectToServer = new(tcpServer.BindAddress!, negotiateClientProtocol);
        ConnectionToServer connectionToServer = await connectToServer.ConnectAsync(cts.Token);
        IServerFunctionality serverFuncProxy = new ServerFunctionalityProxy(connectionToServer);

        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveConnections,
            Is.EqualTo(1).After(1000, 10));

        await serverFuncProxy.CallAsync(cts.Token);

        serverFuncMock.Verify(
            mock => mock.CallAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        connectionToServer.Dispose();
        cts.Cancel();

        await serverTask;

        Assert.That(acceptLoopStartEvents, Is.EqualTo(1));
        Assert.That(acceptLoopStopEvents, Is.EqualTo(1));
        Assert.That(connAcceptEvents, Is.EqualTo(1));
    }

    [Test, Timeout(TestingConstants.Timeout), TestCaseSource(nameof(RpcCapabilitiesCombinations))]
    public async Task Failed_Remote_Procedure_Call_Propagates_Exception(RpcCapabilities capabilities)
    {
        int acceptLoopStartEvents = 0;
        EventHandler<AcceptLoopStartEventArgs> acceptLoopStartEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            Assert.That(args.LaunchCount, Is.EqualTo(acceptLoopStartEvents));
            Assert.That(args.CancelRequested, Is.False);
            acceptLoopStartEvents++;
        };

        int acceptLoopStopEvents = 0;
        EventHandler<AcceptLoopStopEventArgs> acceptLoopStopEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            acceptLoopStopEvents++;
            Assert.That(args.LaunchCount, Is.EqualTo(acceptLoopStopEvents));
        };

        int connAcceptEvents = 0;
        EventHandler<ConnectionAcceptEventArgs> connAcceptEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            connAcceptEvents++;
            Assert.That(args.CancelRequested, Is.False);
        };

        const string exceptionMsg = "This method will be implemented on v3.1";

        Mock<IServerFunctionality> serverFuncMock = new(MockBehavior.Strict);
        serverFuncMock.Setup(
                mock => mock.CallAsync(It.IsAny<CancellationToken>()))
            .Throws(new NotImplementedException(exceptionMsg));

        CancellationTokenSource cts = new();
        cts.CancelAfter(TestingConstants.Timeout);

        INegotiateRpcProtocol negotiateServerProtocol =
            new DefaultServerProtocolNegotiation(
                capabilities,
                capabilities,
                ArrayPool<byte>.Shared,
                mCertificatePath,
                "c3rt1f1c4t3p4ssw0rd");

        INegotiateRpcProtocol negotiateClientProtocol =
            new DefaultClientProtocolNegotiation(
                capabilities,
                RpcCapabilities.None,
                ArrayPool<byte>.Shared,
                DefaultClientProtocolNegotiation.AcceptAllCertificates);

        IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);

        StubCollection stubCollection = new(new ServerFunctionalityStub(serverFuncMock.Object));
        IServer tcpServer = new TcpServer(endPoint, stubCollection, negotiateServerProtocol);
        tcpServer.AcceptLoopStart += acceptLoopStartEventHandler;
        tcpServer.AcceptLoopStop += acceptLoopStopEventHandler;
        tcpServer.ConnectionAccept += connAcceptEventHandler;

        Task serverTask = tcpServer.ListenAsync(cts.Token);

        Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

        ConnectToServer connectToServer = new(tcpServer.BindAddress!, negotiateClientProtocol);
        ConnectionToServer connectionToServer = await connectToServer.ConnectAsync(cts.Token);
        IServerFunctionality serverFuncProxy = new ServerFunctionalityProxy(connectionToServer);

        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveConnections,
            Is.EqualTo(1).After(1000, 10));

        Assert.That(
            async () => await serverFuncProxy.CallAsync(cts.Token),
            Throws.TypeOf<RpcException>()
                .And.Property("Message").Contains(exceptionMsg)
                .And.Property("ExceptionType").Contains(nameof(NotImplementedException))
                .And.Property("StackTrace").Contains(nameof(ServerFunctionalityStub)));

        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveConnections,
            Is.EqualTo(1).After(1000, 10));

        serverFuncMock.Verify(
            mock => mock.CallAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        connectionToServer.Dispose();
        cts.Cancel();

        await serverTask;

        Assert.That(acceptLoopStartEvents, Is.EqualTo(1));
        Assert.That(acceptLoopStopEvents, Is.EqualTo(1));
        Assert.That(connAcceptEvents, Is.EqualTo(1));
    }

    [Test, Timeout(TestingConstants.Timeout), TestCaseSource(nameof(RpcCapabilitiesCombinations))]
    public async Task Not_Supported_Remote_Procedure_Call_Closes_Connection(RpcCapabilities capabilities)
    {
        int acceptLoopStartEvents = 0;
        EventHandler<AcceptLoopStartEventArgs> acceptLoopStartEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            Assert.That(args.LaunchCount, Is.EqualTo(acceptLoopStartEvents));
            Assert.That(args.CancelRequested, Is.False);
            acceptLoopStartEvents++;
        };

        int acceptLoopStopEvents = 0;
        EventHandler<AcceptLoopStopEventArgs> acceptLoopStopEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            acceptLoopStopEvents++;
            Assert.That(args.LaunchCount, Is.EqualTo(acceptLoopStopEvents));
        };

        int connAcceptEvents = 0;
        EventHandler<ConnectionAcceptEventArgs> connAcceptEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            connAcceptEvents++;
            Assert.That(args.CancelRequested, Is.False);
        };

        Mock<IServerFunctionality> serverFuncMock = new(MockBehavior.Strict);

        CancellationTokenSource cts = new();
        cts.CancelAfter(TestingConstants.Timeout);

        INegotiateRpcProtocol negotiateServerProtocol =
            new DefaultServerProtocolNegotiation(
                capabilities,
                capabilities,
                ArrayPool<byte>.Shared,
                mCertificatePath,
                "c3rt1f1c4t3p4ssw0rd");

        INegotiateRpcProtocol negotiateClientProtocol =
            new DefaultClientProtocolNegotiation(
                capabilities,
                RpcCapabilities.None,
                ArrayPool<byte>.Shared,
                DefaultClientProtocolNegotiation.AcceptAllCertificates);

        IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);

        StubCollection stubCollection = new(new ServerFunctionalityStub(serverFuncMock.Object));
        IServer tcpServer = new TcpServer(endPoint, stubCollection, negotiateServerProtocol);
        tcpServer.AcceptLoopStart += acceptLoopStartEventHandler;
        tcpServer.AcceptLoopStop += acceptLoopStopEventHandler;
        tcpServer.ConnectionAccept += connAcceptEventHandler;

        Task serverTask = tcpServer.ListenAsync(cts.Token);

        Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

        ConnectToServer connectToServer = new(tcpServer.BindAddress!, negotiateClientProtocol);
        ConnectionToServer connectionToServer = await connectToServer.ConnectAsync(cts.Token);
        IServerFunctionality serverFuncProxy = new ServerFunctionalityProxy(connectionToServer);

        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveConnections,
            Is.EqualTo(1).After(1000, 10));

        Assert.That(
            async () => await serverFuncProxy.CallUnsupportedAsync(cts.Token),
            Throws.TypeOf<NotSupportedException>().And.Message.Contains("is not supported by the server"));

        Assert.That(
            () => connectionToServer.IsConnected(),
            Is.False.After(1000, 10));

        connectionToServer.Dispose();
        cts.Cancel();

        await serverTask;

        Assert.That(acceptLoopStartEvents, Is.EqualTo(1));
        Assert.That(acceptLoopStopEvents, Is.EqualTo(1));
        Assert.That(connAcceptEvents, Is.EqualTo(1));
    }

    [Test, Timeout(TestingConstants.Timeout)]
    public async Task Cancelling_Accept_Loop_Launch_Causes_Server_To_Exit()
    {
        int acceptLoopStartEvents = 0;
        EventHandler<AcceptLoopStartEventArgs> acceptLoopStartEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            Assert.That(args.LaunchCount, Is.EqualTo(acceptLoopStartEvents));
            Assert.That(args.CancelRequested, Is.False);
            args.CancelRequested = true;
            acceptLoopStartEvents++;
        };

        int acceptLoopStopEvents = 0;
        EventHandler<AcceptLoopStopEventArgs> acceptLoopStopEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            acceptLoopStopEvents++;
            Assert.That(args.LaunchCount, Is.EqualTo(acceptLoopStopEvents));
        };

        Mock<IServerFunctionality> serverFuncMock = new(MockBehavior.Strict);

        CancellationTokenSource cts = new();
        cts.CancelAfter(TestingConstants.Timeout);

        INegotiateRpcProtocol negotiateServerProtocol =
            new DefaultServerProtocolNegotiation(
                RpcCapabilities.None,
                RpcCapabilities.None,
                ArrayPool<byte>.Shared,
                string.Empty,
                string.Empty);

        IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);

        StubCollection stubCollection = new(new ServerFunctionalityStub(serverFuncMock.Object));
        IServer tcpServer = new TcpServer(endPoint, stubCollection, negotiateServerProtocol);
        tcpServer.AcceptLoopStart += acceptLoopStartEventHandler;
        tcpServer.AcceptLoopStop += acceptLoopStopEventHandler;

        Task serverTask = tcpServer.ListenAsync(cts.Token);

        Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

        await serverTask;

        // Server exits without having to cancel the token
        Assert.That(cts.IsCancellationRequested, Is.False);

        Assert.That(acceptLoopStartEvents, Is.EqualTo(1));
        Assert.That(acceptLoopStopEvents, Is.EqualTo(1));
    }

    [Test, Timeout(TestingConstants.Timeout)]
    public async Task Cancelling_Connection_Accept_Causes_Connection_To_Drop()
    {
        int acceptLoopStartEvents = 0;
        EventHandler<AcceptLoopStartEventArgs> acceptLoopStartEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            Assert.That(args.LaunchCount, Is.EqualTo(acceptLoopStartEvents));
            Assert.That(args.CancelRequested, Is.False);
            acceptLoopStartEvents++;
        };

        int acceptLoopStopEvents = 0;
        EventHandler<AcceptLoopStopEventArgs> acceptLoopStopEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            acceptLoopStopEvents++;
            Assert.That(args.LaunchCount, Is.EqualTo(acceptLoopStopEvents));
        };

        int connAcceptEvents = 0;
        bool cancelNextConnection = false;
        EventHandler<ConnectionAcceptEventArgs> connAcceptEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            connAcceptEvents++;
            Assert.That(args.CancelRequested, Is.False);
            args.CancelRequested = cancelNextConnection;
        };

        Mock<IServerFunctionality> serverFuncMock = new(MockBehavior.Strict);

        CancellationTokenSource cts = new();
        cts.CancelAfter(TestingConstants.Timeout);

        INegotiateRpcProtocol negotiateServerProtocol =
            new DefaultServerProtocolNegotiation(
                RpcCapabilities.None,
                RpcCapabilities.None,
                ArrayPool<byte>.Shared,
                mCertificatePath,
                "c3rt1f1c4t3p4ssw0rd");

        INegotiateRpcProtocol negotiateClientProtocol =
            new DefaultClientProtocolNegotiation(
                RpcCapabilities.None,
                RpcCapabilities.None,
                ArrayPool<byte>.Shared,
                DefaultClientProtocolNegotiation.AcceptAllCertificates);

        IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);

        StubCollection stubCollection = new(new ServerFunctionalityStub(serverFuncMock.Object));
        IServer tcpServer = new TcpServer(endPoint, stubCollection, negotiateServerProtocol);
        tcpServer.AcceptLoopStart += acceptLoopStartEventHandler;
        tcpServer.AcceptLoopStop += acceptLoopStopEventHandler;
        tcpServer.ConnectionAccept += connAcceptEventHandler;

        Task serverTask = tcpServer.ListenAsync(cts.Token);

        Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

        ConnectToServer connectToServer = new(tcpServer.BindAddress!, negotiateClientProtocol);
        ConnectionToServer firstConnection = await connectToServer.ConnectAsync(cts.Token);

        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveConnections,
            Is.EqualTo(1).After(1000, 10));

        Assert.That(
            () => firstConnection.IsConnected(),
            Is.True.After(1000, 10));

        cancelNextConnection = true;
        ConnectionToServer secondConnection = await connectToServer.ConnectAsync(cts.Token);

        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveConnections,
            Is.EqualTo(1).After(1000, 10));

        Assert.That(
            () => secondConnection.IsConnected(),
            Is.False.After(1000, 10));

        firstConnection.Dispose();

        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveConnections,
            Is.EqualTo(0).After(1000, 10));

        Assert.That(
            () => firstConnection.IsConnected(),
            Is.False.After(1000, 10));

        secondConnection.Dispose();
        cts.Cancel();

        await serverTask;

        Assert.That(acceptLoopStartEvents, Is.EqualTo(1));
        Assert.That(acceptLoopStopEvents, Is.EqualTo(1));
        Assert.That(connAcceptEvents, Is.EqualTo(2));
    }

    [SetUp]
    public void Setup()
    {
        mCertificatePath = Path.GetTempFileName();
        File.Delete(mCertificatePath);
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(mCertificatePath))
            File.Delete(mCertificatePath);
    }

    string mCertificatePath = string.Empty;

    #region Proxy and stub implementations

    static IEnumerable<RpcCapabilities> RpcCapabilitiesCombinations() =>
        new[]
        {
            RpcCapabilities.None,
            RpcCapabilities.Compression,
            RpcCapabilities.Ssl,
            RpcCapabilities.Compression | RpcCapabilities.Ssl
        };

    class ServerFunctionalityStub : IStub
    {
        internal ServerFunctionalityStub(IServerFunctionality chained)
        {
            mChained = chained;
        }

        bool IStub.CanHandleMethod(IMethodId method)
            => Unsafe.As<DefaultMethodId>(method) == CallAsync_MethodId;

        IEnumerable<IMethodId> IStub.GetHandledMethods()
            => new List<DefaultMethodId>
            {
                CallAsync_MethodId
            };

        async Task<RpcNetworkMessages> IStub.RunMethodCallAsync(
            IMethodId methodId,
            BinaryReader reader,
            Func<CancellationToken> beginMethodRunCallback)
        {
            return Unsafe.As<DefaultMethodId>(methodId).Id switch
            {
                CallAsync_Id => await CallAsync(reader, beginMethodRunCallback),
                _ => throw new NotImplementedException()
            };
        }

        async Task<RpcNetworkMessages> CallAsync(
            BinaryReader reader, Func<CancellationToken> beginMethodRunCallback)
        {
            VoidNetworkMessage req = new();
            req.Deserialize(reader);

            CancellationToken ct = beginMethodRunCallback();
            VoidNetworkMessage res = new();

            ct.ThrowIfCancellationRequested();

            await mChained.CallAsync(ct);

            return new RpcNetworkMessages(req, res);
        }

        readonly IServerFunctionality mChained;
    }

    class ServerFunctionalityProxy : IServerFunctionality
    {
        public ServerFunctionalityProxy(ConnectionToServer connectionToServer)
        {
            mConnToServer = connectionToServer;
        }

        public async Task CallAsync(CancellationToken ct)
        {
            VoidNetworkMessage req = new();
            VoidNetworkMessage res = new();
            RpcNetworkMessages msg = new(req, res);

            await mConnToServer.ProcessMethodCallAsync(
                CallAsync_MethodId, msg, ct);
        }

        public async Task CallUnsupportedAsync(CancellationToken ct)
        {
            VoidNetworkMessage req = new();
            VoidNetworkMessage res = new();
            RpcNetworkMessages msg = new(req, res);

            await mConnToServer.ProcessMethodCallAsync(
                CallUnsupportedAsync_MethodId, msg, ct);
        }

        readonly ConnectionToServer mConnToServer;
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public interface IServerFunctionality
    {
        Task CallAsync(CancellationToken ct);
        Task CallUnsupportedAsync(CancellationToken ct);
    }

    const byte CallAsync_Id = 1;
    const byte CallUnsupportedAsync_Id = 2;

    static readonly DefaultMethodId CallAsync_MethodId =
        new(CallAsync_Id, "CallAsync");

    static readonly DefaultMethodId CallUnsupportedAsync_MethodId =
        new(CallUnsupportedAsync_Id, "CallUnsupportedAsync");

    #endregion
}

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
using NUnit.Framework.Interfaces;

using miloRPC.Core.Client;
using miloRPC.Core.Server;
using miloRPC.Core.Shared;
using miloRPC.Channels.Tcp;
using miloRPC.Serialization;

namespace miloRPC.Tests;

[TestFixture]
public class TcpEndToEndTests
{
    [Test, Timeout(TestingConstants.Timeout), TestCaseSource(nameof(RpcCapabilitiesCombinations))]
    public async Task Remote_Procedure_Call_Works_Ok_End_To_End(
        ConnectionSettings serverSettings, ConnectionSettings clientSettings)
    {
        try
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
            EventHandler<ConnectionAcceptEventArgs<IPEndPoint>> connAcceptEventHandler = (sender, args) =>
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
                new DefaultServerProtocolNegotiation(serverSettings);

            INegotiateRpcProtocol negotiateClientProtocol =
                new DefaultClientProtocolNegotiation(clientSettings);

            IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);

            StubCollection stubCollection = new(new ServerFunctionalityStub(serverFuncMock.Object));
            IServer<IPEndPoint> tcpServer = new TcpServer(endPoint, stubCollection, negotiateServerProtocol);
            tcpServer.AcceptLoopStart += acceptLoopStartEventHandler;
            tcpServer.AcceptLoopStop += acceptLoopStopEventHandler;
            tcpServer.ConnectionAccept += connAcceptEventHandler;

            Task serverTask = tcpServer.ListenAsync(cts.Token);

            Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

            ConnectToTcpServer connectToTcpServer = new(tcpServer.BindAddress!, negotiateClientProtocol);
            ConnectionToServer connectionToServer = await connectToTcpServer.ConnectAsync(cts.Token);
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
        finally
        {
            if (File.Exists(serverSettings.Ssl.CertificatePath))
                File.Delete(serverSettings.Ssl.CertificatePath);
        }
    }

    [Test, Timeout(TestingConstants.Timeout), TestCaseSource(nameof(RpcCapabilitiesCombinations))]
    public async Task Failed_Remote_Procedure_Call_Propagates_Exception(
        ConnectionSettings serverSettings, ConnectionSettings clientSettings)
    {
        try
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
            EventHandler<ConnectionAcceptEventArgs<IPEndPoint>> connAcceptEventHandler = (sender, args) =>
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
                new DefaultServerProtocolNegotiation(serverSettings);

            INegotiateRpcProtocol negotiateClientProtocol =
                new DefaultClientProtocolNegotiation(clientSettings);

            IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);

            StubCollection stubCollection = new(new ServerFunctionalityStub(serverFuncMock.Object));
            IServer<IPEndPoint> tcpServer = new TcpServer(endPoint, stubCollection, negotiateServerProtocol);
            tcpServer.AcceptLoopStart += acceptLoopStartEventHandler;
            tcpServer.AcceptLoopStop += acceptLoopStopEventHandler;
            tcpServer.ConnectionAccept += connAcceptEventHandler;

            Task serverTask = tcpServer.ListenAsync(cts.Token);

            Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

            ConnectToTcpServer connectToTcpServer = new(tcpServer.BindAddress!, negotiateClientProtocol);
            ConnectionToServer connectionToServer = await connectToTcpServer.ConnectAsync(cts.Token);
            IServerFunctionality serverFuncProxy = new ServerFunctionalityProxy(connectionToServer);

            Assert.That(
                () => tcpServer.ActiveConnections.Counters.ActiveConnections,
                Is.EqualTo(1).After(1000, 10));

            Assert.That(
                async () => await serverFuncProxy.CallAsync(cts.Token),
                Throws.TypeOf<SerializableException>()
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
        finally
        {
            if (File.Exists(serverSettings.Ssl.CertificatePath))
                File.Delete(serverSettings.Ssl.CertificatePath);
        }
    }

    [Test, Timeout(TestingConstants.Timeout), TestCaseSource(nameof(RpcCapabilitiesCombinations))]
    public async Task Not_Supported_Remote_Procedure_Call_Does_Not_Close_Connection(
        ConnectionSettings serverSettings, ConnectionSettings clientSettings)
    {
        try
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
            EventHandler<ConnectionAcceptEventArgs<IPEndPoint>> connAcceptEventHandler = (sender, args) =>
            {
                Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
                connAcceptEvents++;
                Assert.That(args.CancelRequested, Is.False);
            };

            Mock<IServerFunctionality> serverFuncMock = new(MockBehavior.Strict);

            CancellationTokenSource cts = new();
            cts.CancelAfter(TestingConstants.Timeout);

            INegotiateRpcProtocol negotiateServerProtocol =
                new DefaultServerProtocolNegotiation(serverSettings);

            INegotiateRpcProtocol negotiateClientProtocol =
                new DefaultClientProtocolNegotiation(clientSettings);

            IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);

            StubCollection stubCollection = new(new ServerFunctionalityStub(serverFuncMock.Object));
            IServer<IPEndPoint> tcpServer = new TcpServer(endPoint, stubCollection, negotiateServerProtocol);
            tcpServer.AcceptLoopStart += acceptLoopStartEventHandler;
            tcpServer.AcceptLoopStop += acceptLoopStopEventHandler;
            tcpServer.ConnectionAccept += connAcceptEventHandler;

            Task serverTask = tcpServer.ListenAsync(cts.Token);

            Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

            ConnectToTcpServer connectToTcpServer = new(tcpServer.BindAddress!, negotiateClientProtocol);
            ConnectionToServer connectionToServer = await connectToTcpServer.ConnectAsync(cts.Token);
            IServerFunctionality serverFuncProxy = new ServerFunctionalityProxy(connectionToServer);

            Assert.That(
                () => tcpServer.ActiveConnections.Counters.ActiveConnections,
                Is.EqualTo(1).After(1000, 10));

            Assert.That(
                async () => await serverFuncProxy.CallUnsupportedAsync(cts.Token),
                Throws.TypeOf<NotSupportedException>().And.Message.Contains("is not supported by the server"));

            Assert.That(
                () => connectionToServer.IsConnected(),
                Is.True.After(1000, 10));

            connectionToServer.Dispose();
            cts.Cancel();

            await serverTask;

            Assert.That(acceptLoopStartEvents, Is.EqualTo(1));
            Assert.That(acceptLoopStopEvents, Is.EqualTo(1));
            Assert.That(connAcceptEvents, Is.EqualTo(1));
        }
        finally
        {
            if (File.Exists(serverSettings.Ssl.CertificatePath))
                File.Delete(serverSettings.Ssl.CertificatePath);
        }
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

        IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);

        StubCollection stubCollection = new(new ServerFunctionalityStub(serverFuncMock.Object));
        IServer<IPEndPoint> tcpServer = new TcpServer(endPoint, stubCollection);
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
        EventHandler<ConnectionAcceptEventArgs<IPEndPoint>> connAcceptEventHandler = (sender, args) =>
        {
            Assert.That(sender, Is.Not.Null.And.InstanceOf<TcpServer>());
            connAcceptEvents++;
            Assert.That(args.CancelRequested, Is.False);
            args.CancelRequested = cancelNextConnection;
        };

        Mock<IServerFunctionality> serverFuncMock = new(MockBehavior.Strict);

        CancellationTokenSource cts = new();
        cts.CancelAfter(TestingConstants.Timeout);

        IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);

        StubCollection stubCollection = new(new ServerFunctionalityStub(serverFuncMock.Object));
        IServer<IPEndPoint> tcpServer = new TcpServer(endPoint, stubCollection);
        tcpServer.AcceptLoopStart += acceptLoopStartEventHandler;
        tcpServer.AcceptLoopStop += acceptLoopStopEventHandler;
        tcpServer.ConnectionAccept += connAcceptEventHandler;

        Task serverTask = tcpServer.ListenAsync(cts.Token);

        Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

        ConnectToTcpServer connectToTcpServer = new(tcpServer.BindAddress!);
        ConnectionToServer firstConnection = await connectToTcpServer.ConnectAsync(cts.Token);

        Assert.That(
            () => tcpServer.ActiveConnections.Counters.ActiveConnections,
            Is.EqualTo(1).After(1000, 10));

        Assert.That(
            () => firstConnection.IsConnected(),
            Is.True.After(1000, 10));

        cancelNextConnection = true;
        ConnectionToServer secondConnection = await connectToTcpServer.ConnectAsync(cts.Token);

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

    #region Proxy and stub implementations

    static IEnumerable<ITestCaseData> RpcCapabilitiesCombinations()
    {
        string certificatePath = Path.GetTempFileName();
        if (File.Exists(certificatePath))
            File.Delete(certificatePath);

        ConnectionSettings.SslSettings serverSslSettings = new()
        {
            Status = SharedCapabilityEnablement.EnabledMandatory,
            CertificatePath = certificatePath,
            CertificatePassword = "c3rt1f1c4t3p4ssw0rd"
        };

        ConnectionSettings.SslSettings clientSslSettings = new()
        {
            Status = SharedCapabilityEnablement.EnabledMandatory,
            CertificateValidationCallback = ConnectionSettings.SslSettings.AcceptAllCertificates
        };

        ConnectionSettings.CompressionSettings compressionSettings = new()
        {
            Status = SharedCapabilityEnablement.EnabledMandatory,
            ArrayPool = ArrayPool<byte>.Shared
        };
        
        ConnectionSettings serverSettings = ConnectionSettings.None;
        ConnectionSettings clientSettings = ConnectionSettings.None;

        yield return new TestCaseData(serverSettings, clientSettings);

        serverSettings = new ConnectionSettings
        {
            Ssl = ConnectionSettings.SslSettings.Disabled,
            Compression = compressionSettings,
            Buffering = ConnectionSettings.BufferingSettings.Disabled
        };
        
        clientSettings = new ConnectionSettings
        {
            Ssl = ConnectionSettings.SslSettings.Disabled,
            Compression = compressionSettings,
            Buffering = ConnectionSettings.BufferingSettings.Disabled
        };

        yield return new TestCaseData(serverSettings, clientSettings);

        serverSettings = new ConnectionSettings
        {
            Ssl = serverSslSettings,
            Compression = ConnectionSettings.CompressionSettings.Disabled,
            Buffering = ConnectionSettings.BufferingSettings.Disabled
        };

        clientSettings = new ConnectionSettings
        {
            Ssl = clientSslSettings,
            Compression = ConnectionSettings.CompressionSettings.Disabled,
            Buffering = ConnectionSettings.BufferingSettings.Disabled
        };

        yield return new TestCaseData(serverSettings, clientSettings);
        
        serverSettings = new ConnectionSettings
        {
            Ssl = serverSslSettings,
            Compression = compressionSettings,
            Buffering = ConnectionSettings.BufferingSettings.Disabled
        };

        clientSettings = new ConnectionSettings
        {
            Ssl = clientSslSettings,
            Compression = compressionSettings,
            Buffering = ConnectionSettings.BufferingSettings.Disabled
        };

        yield return new TestCaseData(serverSettings, clientSettings);

        serverSettings = new ConnectionSettings
        {
            Ssl = ConnectionSettings.SslSettings.Disabled,
            Compression = ConnectionSettings.CompressionSettings.Disabled,
            Buffering = ConnectionSettings.BufferingSettings.EnabledRecommended
        };
        
        clientSettings = new ConnectionSettings
        {
            Ssl = ConnectionSettings.SslSettings.Disabled,
            Compression = ConnectionSettings.CompressionSettings.Disabled,
            Buffering = ConnectionSettings.BufferingSettings.EnabledRecommended
        };

        yield return new TestCaseData(serverSettings, clientSettings);
        
        serverSettings = new ConnectionSettings
        {
            Ssl = serverSslSettings,
            Compression = ConnectionSettings.CompressionSettings.Disabled,
            Buffering = ConnectionSettings.BufferingSettings.EnabledRecommended
        };

        clientSettings = new ConnectionSettings
        {
            Ssl = clientSslSettings,
            Compression = ConnectionSettings.CompressionSettings.Disabled,
            Buffering = ConnectionSettings.BufferingSettings.EnabledRecommended
        };

        yield return new TestCaseData(serverSettings, clientSettings);

        serverSettings = new ConnectionSettings
        {
            Ssl = ConnectionSettings.SslSettings.Disabled,
            Compression = compressionSettings,
            Buffering = ConnectionSettings.BufferingSettings.EnabledRecommended
        };

        clientSettings = new ConnectionSettings
        {
            Ssl = ConnectionSettings.SslSettings.Disabled,
            Compression = compressionSettings,
            Buffering = ConnectionSettings.BufferingSettings.EnabledRecommended
        };
        
        yield return new TestCaseData(serverSettings, clientSettings);

        serverSettings = new ConnectionSettings
        {
            Ssl = serverSslSettings,
            Compression = compressionSettings,
            Buffering = ConnectionSettings.BufferingSettings.EnabledRecommended
        };

        clientSettings = new ConnectionSettings
        {
            Ssl = clientSslSettings,
            Compression = compressionSettings,
            Buffering = ConnectionSettings.BufferingSettings.EnabledRecommended
        };

        yield return new TestCaseData(serverSettings, clientSettings);
    }

    class ServerFunctionalityStub : IStub
    {
        internal ServerFunctionalityStub(IServerFunctionality chained)
        {
            mChained = chained;
        }

        bool IStub.CanHandleMethod(IMethodId method)
            => Unsafe.As<DefaultMethodId>(method) == CallAsyncMethodId;

        IEnumerable<IMethodId> IStub.GetHandledMethods()
            => new List<DefaultMethodId>
            {
                CallAsyncMethodId
            };

        async Task<RpcNetworkMessages> IStub.RunMethodCallAsync(
            IMethodId methodId,
            BinaryReader reader,
            IConnectionContext connCtx,
            Func<CancellationToken> beginMethodRunCallback)
        {
            return Unsafe.As<DefaultMethodId>(methodId).Id switch
            {
                CallAsyncId => await CallAsync(reader, beginMethodRunCallback),
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
                CallAsyncMethodId, msg, ct);
        }

        public async Task CallUnsupportedAsync(CancellationToken ct)
        {
            VoidNetworkMessage req = new();
            VoidNetworkMessage res = new();
            RpcNetworkMessages msg = new(req, res);

            await mConnToServer.ProcessMethodCallAsync(
                CallUnsupportedAsyncMethodId, msg, ct);
        }

        readonly ConnectionToServer mConnToServer;
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public interface IServerFunctionality
    {
        Task CallAsync(CancellationToken ct);
        Task CallUnsupportedAsync(CancellationToken ct);
    }

    const byte CallAsyncId = 1;
    const byte CallUnsupportedAsyncId = 2;

    static readonly DefaultMethodId CallAsyncMethodId =
        new(CallAsyncId, "CallAsync");

    static readonly DefaultMethodId CallUnsupportedAsyncMethodId =
        new(CallUnsupportedAsyncId, "CallUnsupportedAsync");

    #endregion
}

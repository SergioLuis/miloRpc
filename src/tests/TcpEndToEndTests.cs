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

            Mock<IDummyService> serverFuncMock = new(MockBehavior.Strict);
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

            StubCollection stubCollection = new(new DummyServiceStub(serverFuncMock.Object));
            IServer<IPEndPoint> tcpServer = new TcpServer(endPoint, stubCollection, negotiateServerProtocol);
            tcpServer.AcceptLoopStart += acceptLoopStartEventHandler;
            tcpServer.AcceptLoopStop += acceptLoopStopEventHandler;
            tcpServer.ConnectionAccept += connAcceptEventHandler;

            Task serverTask = tcpServer.ListenAsync(cts.Token);

            Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

            ConnectToTcpServer connectToTcpServer = new(tcpServer.BindAddress!, negotiateClientProtocol);
            ConnectionToServer connectionToServer = await connectToTcpServer.ConnectAsync(cts.Token);
            IDummyService serverFuncProxy = new DummyServiceProxy(connectionToServer);

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

            Mock<IDummyService> serverFuncMock = new(MockBehavior.Strict);
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

            StubCollection stubCollection = new(new DummyServiceStub(serverFuncMock.Object));
            IServer<IPEndPoint> tcpServer = new TcpServer(endPoint, stubCollection, negotiateServerProtocol);
            tcpServer.AcceptLoopStart += acceptLoopStartEventHandler;
            tcpServer.AcceptLoopStop += acceptLoopStopEventHandler;
            tcpServer.ConnectionAccept += connAcceptEventHandler;

            Task serverTask = tcpServer.ListenAsync(cts.Token);

            Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

            ConnectToTcpServer connectToTcpServer = new(tcpServer.BindAddress!, negotiateClientProtocol);
            ConnectionToServer connectionToServer = await connectToTcpServer.ConnectAsync(cts.Token);
            IDummyService serverFuncProxy = new DummyServiceProxy(connectionToServer);

            Assert.That(
                () => tcpServer.ActiveConnections.Counters.ActiveConnections,
                Is.EqualTo(1).After(1000, 10));

            Assert.That(
                async () => await serverFuncProxy.CallAsync(cts.Token),
                Throws.TypeOf<RpcException>()
                    .And.Property("Message").Contains(exceptionMsg)
                    .And.Property("ExceptionType").Contains(nameof(NotImplementedException))
                    .And.Property("StackTrace").Contains(nameof(DummyServiceStub)));

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

            Mock<IDummyService> serverFuncMock = new(MockBehavior.Strict);

            CancellationTokenSource cts = new();
            cts.CancelAfter(TestingConstants.Timeout);

            INegotiateRpcProtocol negotiateServerProtocol =
                new DefaultServerProtocolNegotiation(serverSettings);

            INegotiateRpcProtocol negotiateClientProtocol =
                new DefaultClientProtocolNegotiation(clientSettings);

            IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);

            StubCollection stubCollection = new(new DummyServiceStub(serverFuncMock.Object));
            IServer<IPEndPoint> tcpServer = new TcpServer(endPoint, stubCollection, negotiateServerProtocol);
            tcpServer.AcceptLoopStart += acceptLoopStartEventHandler;
            tcpServer.AcceptLoopStop += acceptLoopStopEventHandler;
            tcpServer.ConnectionAccept += connAcceptEventHandler;

            Task serverTask = tcpServer.ListenAsync(cts.Token);

            Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

            ConnectToTcpServer connectToTcpServer = new(tcpServer.BindAddress!, negotiateClientProtocol);
            ConnectionToServer connectionToServer = await connectToTcpServer.ConnectAsync(cts.Token);
            IDummyService serverFuncProxy = new DummyServiceProxy(connectionToServer);

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

        Mock<IDummyService> serverFuncMock = new(MockBehavior.Strict);

        CancellationTokenSource cts = new();
        cts.CancelAfter(TestingConstants.Timeout);

        IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);

        StubCollection stubCollection = new(new DummyServiceStub(serverFuncMock.Object));
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

        Mock<IDummyService> serverFuncMock = new(MockBehavior.Strict);

        CancellationTokenSource cts = new();
        cts.CancelAfter(TestingConstants.Timeout);

        IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);

        StubCollection stubCollection = new(new DummyServiceStub(serverFuncMock.Object));
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

    [Test, Timeout(TestingConstants.Timeout), TestCaseSource(nameof(RpcCapabilitiesCombinations))]
    public async Task Stream_Based_Call_Does_Not_End_Until_Stream_Is_Consumed(
        ConnectionSettings serverSettings, ConnectionSettings clientSettings)
    {
        byte[] streamContent = ArrayPool<byte>.Shared.Rent(4 * 1024);
        try
        {
            Random.Shared.NextBytes(streamContent);

            Mock<IStreamService> serverFuncMock = new(MockBehavior.Strict);
            serverFuncMock.Setup(
                mock => mock.DownloadStreamAsync(
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemoryStream(streamContent));

            CancellationTokenSource cts = new();
            cts.CancelAfter(TestingConstants.Timeout);

            INegotiateRpcProtocol negotiateServerProtocol =
                new DefaultServerProtocolNegotiation(serverSettings);

            INegotiateRpcProtocol negotiateClientProtocol =
                new DefaultClientProtocolNegotiation(clientSettings);

            IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);

            StubCollection stubCollection = new(new StreamServiceStub(serverFuncMock.Object));
            IServer<IPEndPoint> tcpServer = new TcpServer(endPoint, stubCollection, negotiateServerProtocol);

            Task serverTask = tcpServer.ListenAsync(cts.Token);
            
            Assert.That(() => tcpServer.BindAddress, Is.Not.Null.After(1000, 10));

            IConnectToServer connectToServer = new ConnectToTcpServer(tcpServer.BindAddress!, negotiateClientProtocol);
            ConnectionToServer connectionToServer = await connectToServer.ConnectAsync(cts.Token);
            IStreamService serverFuncProxy = new StreamServiceProxy(connectionToServer);
            
            Assert.That(
                () => tcpServer.ActiveConnections.Counters.ActiveConnections,
                Is.EqualTo(1).After(1000).PollEvery(10));
            
            ActiveConnections.ActiveConnection conn =
                tcpServer.ActiveConnections.Connections[0];

            Stream downloadedStream = await serverFuncProxy.DownloadStreamAsync(cts.Token);

            Assert.That(
                () => connectionToServer.CurrentStatus,
                Is.EqualTo(ConnectionToServer.Status.Reading).After(1000).PollEvery(10));

            Assert.That(
                StreamContentEquals(downloadedStream, streamContent),
                Is.True);

            await downloadedStream.DisposeAsync();

            Assert.That(
                () => conn.Connection.CurrentStatus,
                Is.EqualTo(ConnectionFromClient.Status.Idling).After(1000).PollEvery(10));

            Assert.That(
                () => connectionToServer.CurrentStatus,
                Is.EqualTo(ConnectionToServer.Status.Idling).After(1000).PollEvery(10));
            
            cts.Cancel();
            await serverTask;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(streamContent);
            if (File.Exists(serverSettings.Ssl.CertificatePath))
                File.Delete(serverSettings.Ssl.CertificatePath);
        }
    }

    static bool StreamContentEquals(Stream st, byte[] buffer)
    {
        if (st.Length != buffer.Length)
            return false;

        byte[] intermediate = ArrayPool<byte>.Shared.Rent(1024);
        try
        {
            int pos = 0;
            while (pos < st.Length)
            {
                int read = st.Read(intermediate);
                for (int i = 0; i < read; i++)
                {
                    if (buffer[pos + i] != intermediate[i])
                        return false;
                }

                pos += read;
            }

            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(intermediate);
        }
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

    class DummyServiceStub : IStub
    {
        internal DummyServiceStub(IDummyService chained)
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

        readonly IDummyService mChained;
    }

    class StreamServiceStub : IStub
    {
        public StreamServiceStub(IStreamService chained)
        {
            mChained = chained;
        }

        bool IStub.CanHandleMethod(IMethodId method)
        {
            DefaultMethodId dmi = Unsafe.As<DefaultMethodId>(method);
            return dmi >= CallDownloadStreamAsync || dmi <= CallUploadStreamAsync;
        }

        IEnumerable<IMethodId> IStub.GetHandledMethods()
            => new List<DefaultMethodId>
        {
            CallDownloadStreamAsync,
            CallUploadStreamAsync
        };

        async Task<RpcNetworkMessages> IStub.RunMethodCallAsync(
            IMethodId methodId,
            BinaryReader reader,
            IConnectionContext connectionContext,
            Func<CancellationToken> beginMethodRunCallback)
        {
            DefaultMethodId dmi = Unsafe.As<DefaultMethodId>(methodId);
            Func<BinaryReader, Func<CancellationToken>, Task<RpcNetworkMessages>> fn =
                dmi.Id switch
                {
                    DownloadStreamAsync => RunDownloadStreamAsync,
                    UploadStreamAsync => RunUploadStreamAsync,
                    _ => throw new NotImplementedException()
                };

            return await fn(reader, beginMethodRunCallback);
        }

        async Task<RpcNetworkMessages> RunDownloadStreamAsync(
            BinaryReader reader, Func<CancellationToken> beginMethodRunCallback)
        {
            VoidNetworkMessage req = new();
            req.Deserialize(reader);

            Stream result = await mChained.DownloadStreamAsync(beginMethodRunCallback());

            SourceStreamMessage res = new(result.Dispose);
            res.Stream = result;

            return new RpcNetworkMessages(req, res);
        }
        
        async Task<RpcNetworkMessages> RunUploadStreamAsync(
            BinaryReader reader, Func<CancellationToken> beginMethodRunCallback)
        {
            SourceStreamMessage req = new();
            req.Deserialize(reader);

            await mChained.UploadStreamAsync(req.Stream!, beginMethodRunCallback());

            VoidNetworkMessage res = new();

            return new RpcNetworkMessages(req, res);
        }

        readonly IStreamService mChained;
    }

    class DummyServiceProxy : IDummyService
    {
        public DummyServiceProxy(ConnectionToServer connectionToServer)
        {
            mConnToServer = connectionToServer;
        }

        async Task IDummyService.CallAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            VoidNetworkMessage req = new();
            VoidNetworkMessage res = new();
            RpcNetworkMessages msg = new(req, res);

            await mConnToServer.ProcessMethodCallAsync(
                CallAsyncMethodId, msg, ct);
        }

        async Task IDummyService.CallUnsupportedAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            VoidNetworkMessage req = new();
            VoidNetworkMessage res = new();
            RpcNetworkMessages msg = new(req, res);

            await mConnToServer.ProcessMethodCallAsync(
                CallUnsupportedAsyncMethodId, msg, ct);
        }

        readonly ConnectionToServer mConnToServer;
    }

    class StreamServiceProxy : IStreamService
    {
        public StreamServiceProxy(ConnectionToServer connectionToServer)
        {
            mConnToServer = connectionToServer;
        }

        async Task<Stream> IStreamService.DownloadStreamAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            VoidNetworkMessage req = new();
            DestinationStreamMessage res = new();

            await mConnToServer.ProcessMethodCallAsync(
                CallDownloadStreamAsync,
                new RpcNetworkMessages(req, res),
                ct);

            return res.Stream;
        }

        async Task IStreamService.UploadStreamAsync(Stream st, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            SourceStreamMessage req = new();
            req.Stream = st;

            VoidNetworkMessage res = new();

            await mConnToServer.ProcessMethodCallAsync(
                CallUploadStreamAsync,
                new RpcNetworkMessages(req, res),
                ct);
        }

        readonly ConnectionToServer mConnToServer;
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public interface IDummyService
    {
        Task CallAsync(CancellationToken ct);
        Task CallUnsupportedAsync(CancellationToken ct);
    }

    public interface IStreamService
    {
        Task<Stream> DownloadStreamAsync(CancellationToken ct);
        Task UploadStreamAsync(Stream st, CancellationToken ct);
    }

    const byte CallAsyncId = 1;
    const byte CallUnsupportedAsyncId = 2;

    const byte DownloadStreamAsync = 3;
    const byte UploadStreamAsync = 4;

    static readonly DefaultMethodId CallAsyncMethodId =
        new(CallAsyncId, "CallAsync");

    static readonly DefaultMethodId CallUnsupportedAsyncMethodId =
        new(CallUnsupportedAsyncId, "CallUnsupportedAsync");

    static readonly DefaultMethodId CallDownloadStreamAsync =
        new(DownloadStreamAsync, "DownloadStreamAsync");

    static readonly DefaultMethodId CallUploadStreamAsync =
        new(UploadStreamAsync, "UploadStreamAsync");

    #endregion
}

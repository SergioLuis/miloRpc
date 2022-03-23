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
    string mCertificatePath = string.Empty;

    [Test, Timeout(TestingConstants.Timeout), TestCaseSource(nameof(RpcCapabilitiesCombinations))]
    public async Task Remote_Procedure_Call_Works_Ok_End_To_End(RpcCapabilities capabilities)
    {
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

        readonly ConnectionToServer mConnToServer;
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public interface IServerFunctionality
    {
        Task CallAsync(CancellationToken ct);
    }

    const byte CallAsync_Id = 1;
    static readonly DefaultMethodId CallAsync_MethodId = new(CallAsync_Id, "CallAsync");
    #endregion
}

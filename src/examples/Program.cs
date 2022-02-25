﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using dotnetRpc.Client;
using dotnetRpc.Extensions;
using dotnetRpc.Server;
using dotnetRpc.Shared;

namespace Orchestrator.Rpc;

class Program
{
    static async Task<int> Main(string[] args)
    {
        CancellationTokenSource cts = new();

        Task serverTask = RunServer.Run(cts.Token);
        await Task.Delay(3 * 1000); // Wait for the server to listen to requests

        Task client1Task = RunClient.Run("client 1", cts.Token);
        Task client2Task = RunClient.Run("client 2", cts.Token);
        Task client3Task = RunClient.Run("client 3", cts.Token);
        await Task.WhenAll(client1Task, client2Task, client3Task);

        cts.Cancel();
        await serverTask;

        return 0;
    }

    static class RunClient
    {
        public static async Task<bool> Run(string clientName, CancellationToken ct)
        {
            try
            {
                CancellationToken connectCt = ct.CancelLinkedTokenAfter(TimeSpan.FromSeconds(3000));

                DefaultClientProtocolNegotiation protocolNegotiation = new(
                    RpcCapabilities.None,
                    RpcCapabilities.None,
                    Compression.None);

                ConnectToServer connectToServer = new(
                    endpoint,
                    protocolNegotiation,
                    new DefaultWriteMethodId(),
                    new ReadMethodCallResult());

                ConnectionToServer connectionToServer =
                    await connectToServer.ConnectAsync(connectCt);

                IPing pingProxy = new PingProxy(connectionToServer);

                Console.WriteLine("Connection stablished!");

                // await Task.Delay(10_000);
                Console.WriteLine(await pingProxy.PingDirectAsync($"{clientName} Echo 1", ct));
                Console.WriteLine(await pingProxy.PingDirectAsync($"{clientName} Echo 2", ct));
                Console.WriteLine(await pingProxy.PingDirectAsync($"{clientName} Echo 3", ct));
                Console.WriteLine(await pingProxy.PingDirectAsync($"{clientName} Echo 4", ct));
                Console.WriteLine(await pingProxy.PingDirectAsync($"{clientName} Echo 5", ct));

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return false;
            }
        }

        class PingProxy : IPing
        {
            public PingProxy(ConnectionToServer connectionToServer)
            {
                mConnectionToServer = connectionToServer;
            }

            async Task<string> IPing.PingDirectAsync(string pingMessage, CancellationToken ct)
            {
                NonNullableStringMessage req = new();
                req.String = pingMessage;

                NonNullableStringMessage res = new();

                RpcNetworkMessages messages = new(req, res);

                await mConnectionToServer.ProcessMethodCallAsync(
                    Methods.PingDirect,
                    messages,
                    ct);

                return res.String;
            }

            async Task<string> IPing.PingReverseAsync(string pingMessage, CancellationToken ct)
            {
                NonNullableStringMessage req = new();
                req.String = pingMessage;

                NonNullableStringMessage res = new();

                RpcNetworkMessages messages = new(req, res);

                await mConnectionToServer.ProcessMethodCallAsync(
                    Methods.PingReverse,
                    messages,
                    ct);

                return res.String;
            }

            readonly ConnectionToServer mConnectionToServer;
        }

        class ReadMethodCallResult : IReadMethodCallResult
        {
            void IReadMethodCallResult.ReadMethodCallResult(
                BinaryReader reader,
                out bool isResultAvailable,
                out Exception? ex)
            {
                MethodResult methodResult = (MethodResult)reader.ReadByte();
                if (methodResult == MethodResult.Ok)
                {
                    isResultAvailable = true;
                    ex = null;
                    return;
                }

                isResultAvailable = false;
                ex = null;
            }
        }
    }

    static class RunServer
    {
        public static async Task<bool> Run(CancellationToken ct)
        {
            Ping ping = new();
            PingStub pingStub = new(ping);

            StubCollection stubCollection = new();
            stubCollection.RegisterStub(pingStub);
            try
            {
                DefaultServerProtocolNegotiation protocolNegotiation = new(
                    RpcCapabilities.None,
                    RpcCapabilities.None,
                    Compression.None);

                IServer tcpServer = new TcpServer(
                    endpoint,
                    stubCollection,
                    protocolNegotiation,
                    new DefaultReadMethodId(),
                    new WriteMethodCallResult());

                await tcpServer.ListenAsync(ct);
                Console.WriteLine("RunServer ListenAsync task completed without errors");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return false;
            }
        }

        class Ping : IPing
        {
            async Task<string> IPing.PingDirectAsync(
                string pingMessage, CancellationToken ct)
            {
                await Task.Delay(mRandom.Next(1000, 3000), ct);
                return pingMessage;
            }

            async Task<string> IPing.PingReverseAsync(
                string pingMessage, CancellationToken ct)
            {
                await Task.Delay(mRandom.Next(1500, 3000), ct);

                char[] direct = pingMessage.ToCharArray();
                Array.Reverse(direct);
                string result = new(direct);

                return result;
            }

            Random mRandom = new(Environment.TickCount);
        }

        class PingStub : IStub
        {
            delegate Task<RpcNetworkMessages> ProcessMethodCallAsyncDelegate(
                BinaryReader reader,
                Func<CancellationToken> beginMethodRunCallback);

            public PingStub(IPing ping)
            {
                mPing = ping;
                mHandlingFunctions = new()
                {
                    { Methods.PingDirect, PingDirectAsync },
                    { Methods.PingReverse, PingReverseAsync }
                };
            }

            bool IStub.CanHandleMethod(IMethodId method)
                => mHandlingFunctions.ContainsKey(method);

            IEnumerable<IMethodId> IStub.GetHandledMethods()
                => mHandlingFunctions.Keys;

            async Task<RpcNetworkMessages> IStub.RunMethodCallAsync(
                IMethodId methodId,
                BinaryReader reader,
                Func<CancellationToken> beginMethodRunCallback)
            {
                if (!mHandlingFunctions.TryGetValue(
                    methodId, out ProcessMethodCallAsyncDelegate? func))
            {
                throw new NotImplementedException(
                    $"The method {methodId} is not implemented by the Stub");
            }

                return await func(reader, beginMethodRunCallback);
            }

            async Task<RpcNetworkMessages> PingDirectAsync(
                BinaryReader reader,
                Func<CancellationToken> beginMethodRunCallback)
            {
                NonNullableStringMessage req = new();
                req.Deserialize(reader);

                string result = await mPing.PingDirectAsync(
                    req.String, beginMethodRunCallback());

                NonNullableStringMessage res = new();
                res.String = result;

                return new RpcNetworkMessages(req, res);
            }

            async Task<RpcNetworkMessages> PingReverseAsync(
                BinaryReader reader,
                Func<CancellationToken> beginMethodRunCallback)
            {
                NonNullableStringMessage req = new();
                req.Deserialize(reader);

                string result = await mPing.PingReverseAsync(
                    req.String, beginMethodRunCallback());

                NonNullableStringMessage res = new();
                res.String = result;

                return new RpcNetworkMessages(req, res);
            }

            readonly IPing mPing;
            readonly Dictionary<IMethodId, ProcessMethodCallAsyncDelegate> mHandlingFunctions;
        }

        class WriteMethodCallResult : IWriteMethodCallResult
        {
            void IWriteMethodCallResult.WriteFailedMethodCallResult(BinaryWriter writer, Exception ex)
            {
                writer.Write((byte)MethodResult.Failed);
            }

            void IWriteMethodCallResult.WriteNotSupportedMethodCallResult(BinaryWriter writer)
            {
                writer.Write((byte)MethodResult.NotSupported);
            }

            void IWriteMethodCallResult.WriteOkMethodCallResult(BinaryWriter writer)
            {
                writer.Write((byte)MethodResult.Ok);
            }
        }
    }

    interface IPing
    {
        Task<string> PingDirectAsync(string pingMessage, CancellationToken ct);
        Task<string> PingReverseAsync(string pingMessage, CancellationToken ct);
    }

    public static class Methods
    {
        public const byte PingDirectId = 1;
        public const byte PingReverseId = 2;

        public static readonly DefaultMethodId PingDirect = new(PingDirectId, "PingDirect");
        public static readonly DefaultMethodId PingReverse = new(PingReverseId, "PingReverse");
    }

    public enum MethodResult : byte
    {
        Ok = 0,
        NotSupported = 1,
        Failed = 2
    }

    public class NonNullableStringMessage : INetworkMessage
    {
        public string String { get; set; } = string.Empty;

        public void Deserialize(BinaryReader reader)
        {
            String = reader.ReadString();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((string)String);
        }
    }

    static readonly IPEndPoint endpoint = new(IPAddress.Loopback, 7890);
}
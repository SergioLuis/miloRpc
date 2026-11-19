# miloRpc

**Unsurprising RPC for .NET.** A fully async remote procedure calling framework with minimum overhead, that you can read, debug and reason about.

[![NuGet](https://img.shields.io/nuget/vpre/miloRpc.Core?label=miloRpc.Core)](https://www.nuget.org/packages/miloRpc.Core)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/SergioLuis/miloRpc/blob/master/LICENSE.txt)

- **No magic.** No code generation and no reflection. You write a small proxy and stub and you stay in control all the way down to the network layer.
- **Async all the way.** Every call is `async`, cancellable and bounded by configurable timeouts.
- **Secure and lean on the wire.** TLS, Brotli compression and buffering are negotiated per connection, and you choose which are optional and which are mandatory.
- **Pooled connections.** Rent a connection, make the call, give it back. Calls run in parallel.
- **Your logger.** Built on `Microsoft.Extensions.Logging`, so it plugs into NLog, Serilog or anything else.

```text
    ┌────────────────────┐       ▲ ┌────────────────────┐
    │Client code         │ │     │ │         Server code│  your application code
    ├────────────────────┤ │     │ ├────────────────────┤  ─────────────────────
    │Client proxy        │ │     │ │         Server stub│           boilerplate
    ├────────────────────┤ │     │ ├────────────────────┤  ─────────────────────
    │miloRpc abstractions│ │     │ │miloRpc abstractions│
    ├────────────────────┤ │     │ ├────────────────────┤                miloRpc
    │Channel (network)   │ │     │ │   Channel (network)│
    └────────────────────┘ ▼       └────────────────────┘
```

## Packages

| Package | What it gives you | Target |
|---|---|---|
| `miloRpc.Core` | Abstractions, connection pool, stubs, protocol negotiation. **Always required.** | net6.0 |
| `miloRpc.Channels.Tcp` | Channel over TCP and TCP + TLS. The one most people want. | net6.0 |
| `miloRpc.Channels.Quic` | Channel over [QUIC](https://en.wikipedia.org/wiki/QUIC). TLS is always on. | net7.0 |
| `miloRpc.Serialization` | `NetworkMessage<T>` and serializers for primitives, nullables, lists, tuples, `Guid`, `DateTime`, memory buffers... | net6.0 |

```powershell
dotnet add package miloRpc.Core --version 1.1.1-beta
dotnet add package miloRpc.Channels.Tcp --version 1.1.1-beta
dotnet add package miloRpc.Serialization --version 1.1.1-beta
```

## Quick start

A server that shouts back whatever it receives, over TCP.

### 1. Identify your methods (shared by client and server)

```csharp
static class Methods
{
    public static readonly DefaultMethodId Shout = new(1, "Shout");
}
```

`DefaultMethodId` uses one `byte`, so you get up to 255 methods. Implement `IMethodId` if you need more.

### 2. Server: write a stub

The stub is the bridge between miloRpc and your logic. It declares which methods it handles, reads the request, and returns the response.

```csharp
class ShoutStub : IStub
{
    bool IStub.CanHandleMethod(IMethodId method)
        => Unsafe.As<DefaultMethodId>(method) == Methods.Shout;

    IEnumerable<IMethodId> IStub.GetHandledMethods()
        => new IMethodId[] { Methods.Shout };

    Task<RpcNetworkMessages> IStub.RunMethodCallAsync(
        IMethodId methodId,
        BinaryReader reader,
        IConnectionContext connectionContext,
        Func<CancellationToken> beginMethodRunCallback)
    {
        NetworkMessage<string> request = new();
        request.Deserialize(reader);

        // Call this once the request is read. The returned token is cancelled
        // when the execution timeout expires.
        beginMethodRunCallback();

        NetworkMessage<string> response = new(request.Val1!.ToUpperInvariant());
        return Task.FromResult(new RpcNetworkMessages(request, response));
    }
}
```

You don't need a `try/catch`. If your code throws, the framework serializes the exception back to the client.

### 3. Client: write a proxy

```csharp
class ShoutProxy
{
    public ShoutProxy(ConnectionPool pool) => mPool = pool;

    public async Task<string> ShoutAsync(string message, CancellationToken ct)
    {
        ConnectionToServer conn = await mPool.RentConnectionAsync(ct);
        try
        {
            NetworkMessage<string> request = new(message);
            NetworkMessage<string> response = new();

            await conn.ProcessMethodCallAsync(
                Methods.Shout, new RpcNetworkMessages(request, response), ct);

            return response.Val1!;
        }
        finally
        {
            mPool.ReturnConnection(conn);   // always give the connection back
        }
    }

    readonly ConnectionPool mPool;
}
```

### 4. Wire it up

```csharp
IPEndPoint endPoint = new(IPAddress.Loopback, 9999);
using CancellationTokenSource cts = new();

// Server
IServer<IPEndPoint> server = new TcpServer(endPoint, new StubCollection(new ShoutStub()));
Task serverTask = server.ListenAsync(cts.Token);

// Client
ConnectionPool pool = new(new ConnectToTcpServer(endPoint), minimumPooledConnections: 2);
await pool.WarmupPool();   // optional: open the first connections up front

Console.WriteLine(await new ShoutProxy(pool).ShoutAsync("hello, miloRpc!", cts.Token));
// HELLO, MILORPC!
```

## TLS, compression and buffering

Capabilities are described with `ConnectionSettings` and negotiated when a client connects. Each side declares a capability as `Disabled`, `EnabledOptional` or `EnabledMandatory`. If one side requires something the other has disabled, the connection is refused.

```csharp
ConnectionSettings serverSettings = new()
{
    Ssl = new ConnectionSettings.SslSettings
    {
        Status = SharedCapabilityEnablement.EnabledMandatory,
        CertificatePath = "/path/to/server.pfx",
        CertificatePassword = "..."            // omit for a passwordless .pfx
    },
    Compression = new ConnectionSettings.CompressionSettings
    {
        Status = SharedCapabilityEnablement.EnabledOptional   // Brotli
    },
    Buffering = ConnectionSettings.BufferingSettings.EnabledRecommended
};

IServer<IPEndPoint> server = new TcpServer(
    endPoint,
    new StubCollection(new ShoutStub()),
    new DefaultServerProtocolNegotiation(serverSettings));
```

The client mirrors it. With a certificate your clients don't trust, say so explicitly:

```csharp
ConnectionSettings clientSettings = new()
{
    Ssl = new ConnectionSettings.SslSettings
    {
        Status = SharedCapabilityEnablement.EnabledMandatory,
        // Only for self-signed certificates you control. Never for untrusted servers.
        CertificateValidationCallback = ConnectionSettings.SslSettings.AcceptAllCertificates
    }
};

IConnectToServer connect = new ConnectToTcpServer(
    endPoint, new DefaultClientProtocolNegotiation(clientSettings));
```

### Certificates, with or without a password

The server loads its certificate from `CertificatePath`:

| `.pfx` file | `CertificatePassword` | Result |
|---|---|---|
| Protected | correct | loads |
| Protected | missing or wrong | **fails at startup** with a clear error |
| Passwordless | omitted, `null` or `""` | loads *(new in 1.1.1-beta)* |
| Does not exist | set | a self-signed certificate is generated and saved there |
| Does not exist | omitted | fails: a password is needed to export a generated certificate |

> **1.1.1-beta** fixes passwordless certificates. Earlier versions threw `SSL is necessary but no cert. password is set...` even when the file already existed and needed no password.

## Custom types

`NetworkMessage<T>` (1 to N values) serializes primitives out of the box. For your own types, implement `ISerializer<T>` and register it once at startup. Register the serializers of leaf types first.

```csharp
class OrderSerializer : ISerializer<Order>
{
    public OrderSerializer()
    {
        mGuid = Serializer<Guid>.Instance;
        mDate = Serializer<DateTime>.Instance;
    }

    public Order? Deserialize(BinaryReader reader)
        => new Order { Id = mGuid.Deserialize(reader), Date = mDate.Deserialize(reader) };

    public void Serialize(BinaryWriter writer, Order? o)
    {
        mGuid.Serialize(writer, o!.Id);
        mDate.Serialize(writer, o.Date);
    }

    readonly ISerializer<Guid> mGuid;
    readonly ISerializer<DateTime> mDate;
}

Serializers.Instance.RegisterInstance(new OrderSerializer());
```

Fields must be read in the same order they were written, and serializers must be thread safe. Mind backwards and forwards compatibility when you change a shared model.

## Logging

miloRpc logs through `Microsoft.Extensions.Logging`. Give it your factory once at startup:

```csharp
RpcLoggerFactory.RegisterLoggerFactory(loggerFactory);
```

## Good to know

- **Beta.** The library is used in production, but versions are `-beta` and the API can still change between releases.
- **QUIC** needs .NET 7 and a platform with MsQuic support (Windows, Linux, macOS). See the [repository](https://github.com/SergioLuis/miloRpc) for the details.
- **Protocol negotiation is replaceable.** Implement `INegotiateRpcProtocol` to add your own rules. You receive the remote `IPEndPoint`, which makes it a good place for an allowlist or denylist.
- **Duplicate methods halt the app.** If two stubs claim the same method id, miloRpc stops instead of risking a logic error that could lose data.

## Links

- Source, issues and a complete QUIC example: <https://github.com/SergioLuis/miloRpc>
- License: [MIT](https://github.com/SergioLuis/miloRpc/blob/master/LICENSE.txt)

Copyright © 2022 Sergio Luis Para

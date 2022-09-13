# miloRpc - Understandable RPC for .NET

miloRpc is a fully async Remote Procedure Calling framework for .NET, with minimum overhead and easy to use and understand.

## Getting miloRpc

miloRpc does not rely in code generation to work. This means that you will need a little bit more boilerplate code here and there, but you will also understand what is going on under the hood at every single step.

> _In the future, miloRpc will support coding client and server-side like modern ASP.NET Controllers - just decorate your methods with some attributes and the framework will do the magic for you. But even then you will be able to decide how much of the stack is under your control, and how much you want miloRpc to take care of._

miloRpc is split accross several packages. You will need the `miloRpc.Core` package, and **at least** one _channel_ package, which will most probably be `miloRpc.Channels.Tcp` (for connections over TCP and TCP/SSL).

```powershell
> dotnet add package miloRpc.Core --version 1.1.0-beta
> dotnet add package miloRpc.Channels.Tcp --version 1.1.0-beta
```

If you want some helper classes to ease serialization and deserialization, you can also add the `miloRpc.Serialization` package:

```powershell
> dotnet add package miloRpc.Serialization --version 1.1.0-beta
```

## General roadmap for miloRpc

Whilst the library is at a production-ready status, many things are ahead for miloRpc. This is a one hundred feet look at the immediate roadmap. Bear in mind that some of these items might introduce API-breaking changes.

- The package `miloRpc.Channels.Quic` will be released as soon as .NET 7 releases with [QUIC](https://en.wikipedia.org/wiki/QUIC) stream support.
- The package `miloRpc.Channels.Udt` will support creating channels on top of a completely managed [UDT](https://en.wikipedia.org/wiki/UDT) sockets implementation.
- More compression algorithms will be available for compression at the channel level.
  - Different content compresses different with different algorithms. You will be able to choose among different ones, or provide an implementation to integrate one yourself.
- It will be possible to enable, disable, and switch compression algorithms at the channel level on the fly.
  - Right now, compression is negotiated for the entire lifetime of the connection.
  - Different type of content might have different compression needs.
  - If you know you are going to send content that doesn't compress, or doesn't compress well, it is not worth it to spend CPU time, you should be able to temporarily disable compression altogether.
- Clients will be able to do client-side load balancing.
  - Bear in mind that for this to work your server side application must support the client transition...!
- Server and client will provide percentile times for method invocations

## How to start using miloRpc

The following sections introduce a **light** example so you can dip your toes in. More in-depth documentation will be available soon.

### Server-side

Your server-side functionality is provided by `IStub` implementations. Every `IStub` implementation is able to run a series of methods that you will have to define and identify. Every method is identified by a `IMethodId` instance (which will most probably be a `static readonly` member somewhere in a class shared between your client and server implementations). The client uses this `IMethodId`, along with the necessary arguments, to invoke a remote method.

miloRpc provides the `IMethodId` interface to implement your method identifiers, but also provides a `DefaultMethodId` implementation that uses a `byte` to differentiate methods - which means that you can have up to 255 different methods if you use `DefaultMethodId`.

```csharp
public static class EchoServerMethodIds
{
    public static readonly DefaultMethodId DirectEcho = new(1, "DirectEcho");
    public static readonly DefaultMethodId ReverseEcho = new(2, "ReverseEcho");
}
```

The `IStub` implementation is a bridge between your actual logic and the miloRpc framework, so it is discouraged to add application-specific logic there. Most probably you will have a shared interface between the client and the server, that the server implements and the client consumes or uses in some manner. Some shared models too - beware of backwards and forwards compatibility when doing changes!

The client part of miloRpc forces async calls to the framework - if your interface is shared between the server and the client, and the server-side implementation is not async, it might lead to some weirdness, so you might want to split the interface in a server interface with sync methods and a client interface with async methods. In this example, what should be sync is async just for the example's sake.

```csharp
public class EchoResponse
{
    public DateTime ReceiveDate { get; set; }
    public string Message { get; set; }
}

public interface IEcho
{
    Task<EchoResponse> DirectAsync(string message, CancellationToken ct);
    Task<EchoResponse> ReverseAsync(string message, CancellationToken ct);
}
```

Then your server implements the interface somehow:

```csharp
public class EchoLogic : IEcho
{
    async Task<EchoResponse> IEcho.DirectAsync(string message, CancellationToken ct)
    {
        await Task.Delay(100, ct);

        return new EchoResponse
        {
            ReceiveDate = DateTime.UtcNow,
            Message = message
        };
    }

    async Task<EchoResponse> IEcho.ReverseAsync(string message, CancellationToken ct)
    {
        await Task.Delay(100, ct);

        char[] msg = message.ToCharArray();
        msg.Reverse();

        return new EchoResponse
        {
            ReceiveDate = DateTime.UtcNow,
            Message = new string(msg)
        };
    }
}
```

Finally, you fill the gap between miloRpc and the service by implementing a `IStub` that declares that it can handle the required methods. As a recommendation, the `IStub` implementation should rely on the abstraction rather than in the specific implementation to ease testing.

```csharp
public EchoStub : IStub
{
    public EchoStub(IEcho service)
    {
        mService = service;
    }

    bool IStub.CanHandleMethod(IMethodId method)
    {
        // Remember to cast the IMethodId to the correct type if you decide to 
        // use your own IMethodId implementation
        DefaultMethodId dmi = Unsafe.As<DefaultMethodId>(method);

        // DefaultMethodId overrides all operators
        // This eases CanHandleMethod implementations by distributing methods
        // in consecutive ranges and just checking for the range limits
        return dmi >= EchoServerMethodIds.DirectEcho
            && dmi <= EchoServerMethodIds.ReverseEcho;
    }

    IEnumerable<IMethodId> IStub.GetHandledMethods()
    {
        // If more than one stub declares that can handle the same method,
        // miloRpc will cause your application to HALT to prevent a logic error
        // that could potentially lead to data loss
        return new IMethodId[]
        {
            EchoServerMethodIds.DirectEcho,
            EchoServerMethodIds.ReverseEcho
        };
    }

    Task<RpcNetworkMessages> IStub.RunMethodCallAsync(
        IMethodId methodId,
        BinaryReader reader,
        IConnectionContext connectionContext,
        Func<CancellationToken> beginMethodRunCallback)
    {
        DefaultMethodId dmi = Unsafe.As<DefaultMethodId>(methodId);

        return dmi switch
        {
            EchoServerMethodIds.DirectEcho
                => await RunDirectEchoAsync(reader, beginMethodRunCallback),

            EchoServerMethodIds.ReverseEcho
                => await RunReverseEchoAsync(reader, beginMethodRunCallback),

            // Can't happen because the framework will prevent calling the IStub
            // with a method that the IStub does not declare, but this construct
            // requires a default case to build
            _ => throw new NotSupportedException("Unknown method")
        };
    }

    // You don't need to wrap these methods in a try/catch block
    // If an exception happens in your code, the framework will pick it up
    // and serialize it back to the client, so you can safely propagate
    // exceptions and stack traces
    Task<RpcNetworkMessages> RunDirectEchoAsync(
        BinaryReader reader,
        Func<CancellationToken> beginMethodRunCallback)
    {
        // You can only use generic NetworkMessages when using the
        // miloRpc.Serialization package.
        // These messages handle serialization and deserialization in a
        // transparent way - as long as the adequate ISerializer implementations
        // are in place (more on this following the example).
        // Otherwise you need to implement your own messages derived from the
        // INetworkMessage interface
        NetworkMessage<string> request = new();
        request.Deserialize(reader);

        // The callback must be called once the request is deserialized
        // This allows the framework to track read and execution times, and the
        // returned CancellationToken will be cancelled after the execution
        // timeout configured when starting up the miloRpc server expires
        CancellationToken ct = beginMethodRunCallback();

        EchoResponse result = await mService.DirectAsync(request.Val1, ct);

        // If you don't have serializers for the model you want to send
        // accross the network, you can use NetworkMessage with primitive types
        // and assign the members of your complex objects one by one
        NetworkMessage<DateTime, string> response = new();
        response.Val1 = result.ReceiveDate;
        response.Val2 = result.Message;

        return new RpcNetworkMessages(request, response);
    }

    Task<RpcNetworkMessages> RunReverseEchoAsync(
        BinaryReader reader,
        Func<CancellationToken> beginMethodRunCallback)
    {
        NetworkMessage<string> request = new();
        request.Deserialize(reader);

        CancellationToken ct = beginMethodRunCallback();

        EchoResponse result = await mService.ReverseAsync(request.Val1, ct);

        // For this to work you need to code and register a ISerializer<EchoResponse>
        NetworkMessage<EchoResponse> response = new();
        response.Val1 = result;

        return new RpcNetworkMessages(request, response);
    }

    readonly IEcho mService;
}
```

In the example above, in `RunReverseEcho` we use a `NetworkMessage<T>` with a non-primitive type. For this to work, you need to write and register the serializer for `T` yourself:

```csharp
// All ISerializer implementations should be thread safe
public class EchoResponseSerializer : ISerializer<EchoResponse>
{
    public EchoResponseSerializer()
    {
        // Bear in mind that you can only access already registered serializers
        //
        // When having a dependency tree, first register the serializers for
        // the leaf objects, then for those that depend on them
        mDateTimeSerializer = Serializers.Get<DateTime>();
        mStringSerializer = Serializers.Get<string>();
    }

    EchoResponse ISerializer<EchoResponse>.Deserialize(BinaryReader reader)
    {
        // The fields must be read in the same order they were written
        return new EchoResponse
        {
            ReceiveDate = mDateTimeSerializer.Deserialize(reader),
            Message = mStringSerializer.Deserialize(reader)
        };
    }

    void ISerializer<EchoResponse>.Serialize(BinaryWriter writer, EchoResponse t)
    {
        // The fields must be read in the same order they were written
        mDateTimeSerializer.Serialize(writer, t.ReceiveDate);
        mStringSerializer.Serialize(writer, t.Message);
    }

    readonly ISerializer<DateTime> mDateTimeSerializer;
    readonly ISerializer<string> mStringSerializer;
}

public static async Task Main(string[] args)
{
    // Some code...
    Serializers.RegisterInstance(new EchoResponseSerializer());
    // Some more code...
}
```

Now that you have the service, the `IStub` to access it, and the necessary serialization mechanism in place for your custom objects, you can start the server!

```csharp
public static async Task Main(string[] args)
{
    // Some code...
    Serializers.RegisterInstance(new EchoResponseSerializer());
    // Some more code...

    StubCollection stubs = new(new EchoStub(new EchoLogic()));

    IPEndPoint serverEndPoint = IPEndPoint.Parse("0.0.0.0:9999");

    // To use the TcpServer you need miloRpc.Channels.Tcp
    IServer<IPEndPoint> server = new TcpServer(serverEndPoint, stubs);

    CancellationTokenSource cts = new();
    Console.CancelKeyPress += (_, e) =>
    {
        Console.WriteLine("Cancellation requested!");
        cts.Cancel();
        e.Cancel = true;
    }

    // The task will finish when the token gets cancelled
    Console.WriteLine("Press CTRL + C to exit");
    await server.ListenAsync(cts.Token);
}
```

### Client-side

The client equivalent to the `IStub` is the proxy. However, clients have no proxy interface to implement - proxies implement the interface shared with the server, if any. As explained before, the miloRpc framework forces calls to the server to be async. If your server-side code is sync, you might want to avoid shared interfaces to prevent weirdness (or even don't have any interface at all...!)

At this point, what you need to invoke methods on the server is a `ConnectionToServer` instance, that you can either build manually (with a `IConnectToServer` implementation) or get through pooling (`ConnectionPool`, but you will need a `IConnectToServer` instance to build one of those).

```csharp
public class EchoProxy : IEcho
{
    public EchoProxy(ConnectionPool connPool)
    {
        // Each ConnectionPool instance pools connections to a single server / endpoint
        mPool = connPool;
    }

    async Task<EchoResponse> IEcho.DirectAsync(string message, CancellationToken ct)
    {
        ConnectionToServer conn = mPool.RentConnectionAsync(TimeSpan.Zero, ct);
        try
        {
            NetworkMessage<string> request = new();
            request.Val1 = message;

            NetworkMessage<DateTime, string> response = new();

            RpcNetworkMessages messages = new(request, response);

            // By the time ProcessMethodCallAsync ends, 'response' will be
            // deserialized with what the server sent
            await conn.ProcessMethodCallAsync(
                EchoServerMethodIds.DirectEcho,
                messages,
                ct);

            return new EchoResponse
            {
                ReceiveDate = response.Val1,
                Message = response.Val2
            };
        }
        finally
        {
            // Remember to return your connection to the pool!
            mPool.ReturnConnection(conn);
        }
    }

    async Task<EchoResponse> IEcho.ReverseAsync(string message, CancellationToken ct)
    {
        ConnectionToServer conn = mPool.RentConnectionAsync(TimeSpan.Zero, ct);
        try
        {
            NetworkMessage<string> request = new();
            request.Val1 = message;

            NetworkMessage<EchoResponse> response = new();

            RpcNetworkMessages messages = new(request, response);

            await conn.ProcessMethodCallAsync(
                EchoServerMethodIds.ReverseEcho,
                messages,
                ct);

            return response.Val1;
        }
        finally
        {
            mPool.ReturnConnection(conn);
        }
    }

    readonly ConnectionPool mPool;
}
```

In order to create a basic `IConnectToServer` instance, you only need the IP address where the miloRpc server is binded to:

```csharp
public static async Task Main(string[] args)
{
    IPEndPoint serverEndPoint = IPEndPoint.Parse("192.168.1.10:9999");
    IConnectToServer connectToServer = new ConnectToTcpServer(serverEndPoint);
    ConnectionPool pool = new ConnectionPool(
        connectToServer,
        minimumPooledConnections: 2);

    // If you want to eat up the time of creating new connections at the beginning...
    await pool.WarmUpPool();

    CancellationTokenSource cts = new();
    cts.CancelAfter(TimeSpan.FromSeconds(2));

    IEcho echo = new EchoProxy(pool);

    // Thanks to the ConnectionPool we can make calls in parallel
    Task<EchoResponse> directResponseTask =
        echo.DirectAsync("hello world!", cts.Token);

    Task<EchoResponse> reverseResponseTask =
        echo.ReverseAsync("!dlrow olleh", cts.Token);

    await Task.WhenAll(directResponseTask, reverseResponseTask);

    Console.WriteLine(directResponseTask.Result.Message);
    Console.WriteLine(reverseResponseTask.Result.Message);
}
```

### Protocol negotiation

miloRpc allows you to override the default protocol negotiation by implementing the `INegotiateRpcProtocol` interface. However, it includes a default protocol negotiation implementation both for client and server side that offers negotiating:

- Compression on the transport layer, on top of the Brotli compression algorithm
- SSL
- Buffering on the transport layer, to speed up reads and writes

These capabilities are defined through the `ConnectionSettings` class, and used when creating a `TcpServer` or `ConnectToTcpServer` instances:

```csharp
var connSettings = new ConnectionSettings
{
    CompressionSettings = new()
    {
        Status = SharedCapabilityEnablement.Optional,
        ArrayPool = ArrayPool<byte>.Shared
    },
    SslSettings = new()
    {
        Status = SharedCapabilityEnablement.Mandatory,
        CertificatePath = "/path/to/my/file.pfx",
        CertificatePassword = "1234",
        ApplicationProtocols = new List<SslApplicationProtocol>()
        {
            new SslApplicationProtocol("echo-server-protocol")
        }
    },
    BufferingSettings = new()
    {
        Status = PrivateCapabilityEnablement.Enabled,
        BufferSize = 4096
    }
};

INegotiateRpcProtocol negotiateRpcProtocol =
    new DefaultServerProtocolNegotiation(connSettings)

IServer<IPEndPoint> tcpServer =
    new TcpServer(bindTo, stubCollection, negotiateRpcProtocol);
```

In this example, the server declares compression as optional and SSL as mandatory:

- If the client declares compression as optional or mandatory, compression will be used.
- If the client declares compression as disabled, compression won't be used.
- If the client declares SSL as optional or mandatory, SSL will be used.
- If the client declares SSL as disabled, the negotiation **will fail** and the connection won't get stablished.

The default negotiation mechanism is good enough for 99% of the cases. However, writing your own protocol negotiation might be useful. At the moment of the negotiation you get the IPEndPoint of the other side, so it is a good place to implement an allowlist/denylist mechanism.

### Using your logging library

miloRpc doesn't rely on a specific logging library. Instead, it relies on Microsoft's logging abstractions.

In order to integrate miloRpc into your logs, you need to provide a `ILoggerFactory` implementation. Some modern frameworks such as NLog provide `ILoggerFactory` implementations out of the box, whilst for others such as log4net you will need to code the abstraction yourself or find a third party library or NuGet package that provides it.

```csharp
ILoggerFactory loggerFactory = MyMagicMethod();
RpcLoggerFactory.RegisterLoggerFactory(loggerFactory);
```

## LICENSE

MIT License

Copyright (c) 2022 Sergio Luis Para

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

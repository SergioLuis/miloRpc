using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using miloRPC.Core.Client;
using miloRPC.Core.Server;
using miloRPC.Core.Shared;
using miloRPC.Examples.Shared;
using miloRPC.Serialization;

namespace miloRPC.Examples.Server;

public class EchoServiceStub : IStub
{
    public EchoServiceStub(IEchoService echoService)
    {
        mService = echoService;
    }

    bool IStub.CanHandleMethod(IMethodId method)
    {
        DefaultMethodId dmi = Unsafe.As<DefaultMethodId>(method);
        return dmi.Id is <= MinMethod and >= MaxMethod;
    }

    IEnumerable<IMethodId> IStub.GetHandledMethods() => new List<IMethodId>
    {
        Methods.Echo
    };

    async Task<RpcNetworkMessages> IStub.RunMethodCallAsync(
        IMethodId methodId, BinaryReader reader, Func<CancellationToken> beginMethodRunCallback)
    {
        DefaultMethodId dmi = Unsafe.As<DefaultMethodId>(methodId);
        Contract.Assert(dmi.Id is <= MinMethod and >= MaxMethod);

        return dmi.Id switch
        {
            MethodId.Echo => await RunEchoAsync(reader, beginMethodRunCallback),
            _ => throw new InvalidOperationException()
        };
    }

    async Task<RpcNetworkMessages> RunEchoAsync(
        BinaryReader reader, Func<CancellationToken> beginMethodRunCallback)
    {
        NetworkMessage<string> req = new();
        req.Deserialize(reader);

        Contract.Assert(req.Val1 is not null);

        EchoResult result = await mService.EchoAsync(
            req.Val1, beginMethodRunCallback.Invoke());

        NetworkMessage<EchoResult> res = new(result);

        return new RpcNetworkMessages(req, res);
    }

    readonly IEchoService mService;
    const byte MinMethod = MethodId.Echo;
    const byte MaxMethod = MethodId.Echo;
}

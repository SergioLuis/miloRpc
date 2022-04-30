using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using dotnetRpc.Core.Server;
using dotnetRpc.Core.Shared;
using dotnetRpc.Serialization;

namespace dotnetRpc.Tests;

public class VoidCallStub : IStub
{
    bool IStub.CanHandleMethod(IMethodId method)
        => Unsafe.As<DefaultMethodId>(method).Id == 1;

    IEnumerable<IMethodId> IStub.GetHandledMethods()
        => new List<DefaultMethodId>()
        {
            new DefaultMethodId(1, "CallAsync")
        };

    async Task<RpcNetworkMessages> IStub.RunMethodCallAsync(
        IMethodId methodId, BinaryReader reader, Func<CancellationToken> beginMethodRunCallback)
    {
        DefaultMethodId defaultMethodId = Unsafe.As<DefaultMethodId>(methodId);

        switch (defaultMethodId.Id)
        {
            case 1:
                return await CallAsync(reader, beginMethodRunCallback);
        }

        throw new NotImplementedException();
    }

    Task<RpcNetworkMessages> CallAsync(
        BinaryReader reader, Func<CancellationToken> beginMethodRunCallback)
    {
        VoidNetworkMessage req = new();
        req.Deserialize(reader);

        CancellationToken ct = beginMethodRunCallback();
        VoidNetworkMessage res = new();

        ct.ThrowIfCancellationRequested();

        return Task.FromResult(new RpcNetworkMessages(req, res));
    }
}

public class LongRunningVoidCallStub : IStub
{
    public void Set() => mSemaphore.Wait();

    public void Reset() => mSemaphore.Release();

    bool IStub.CanHandleMethod(IMethodId method)
        => Unsafe.As<DefaultMethodId>(method).Id == 1;

    IEnumerable<IMethodId> IStub.GetHandledMethods()
        => new List<DefaultMethodId>()
        {
            new DefaultMethodId(1, "LongRunningCallAsync")
        };

    async Task<RpcNetworkMessages> IStub.RunMethodCallAsync(
        IMethodId methodId, BinaryReader reader, Func<CancellationToken> beginMethodRunCallback)
    {
        DefaultMethodId defaultMethodId = Unsafe.As<DefaultMethodId>(methodId);

        switch (defaultMethodId.Id)
        {
            case 1:
                return await CallAsync(reader, beginMethodRunCallback);
        }

        throw new NotImplementedException();
    }

    async Task<RpcNetworkMessages> CallAsync(
        BinaryReader reader, Func<CancellationToken> beginMethodRunCallback)
    {
        VoidNetworkMessage req = new();
        req.Deserialize(reader);

        CancellationToken ct = beginMethodRunCallback();
        VoidNetworkMessage res = new();

        await mSemaphore.WaitAsync(ct);
        mSemaphore.Release();

        ct.ThrowIfCancellationRequested();

        return new RpcNetworkMessages(req, res);
    }

    readonly SemaphoreSlim mSemaphore = new(1);
}

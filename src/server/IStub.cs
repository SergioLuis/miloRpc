using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using dotnetRpc.Shared;

namespace dotnetRpc.Server;

public interface IStub
{
    bool CanHandleMethod(IMethodId method);

    IEnumerable<IMethodId> GetHandledMethods();

    Task<RpcNetworkMessages> RunMethodCallAsync(
        IMethodId methodId,
        BinaryReader reader,
        Func<CancellationToken> beginMethodRunCallback);
}

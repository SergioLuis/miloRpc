using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Server;

public interface IStub
{
    bool CanHandleMethod(IMethodId method);

    IEnumerable<IMethodId> GetHandledMethods();

    Task<RpcNetworkMessages> RunMethodCallAsync(
        IMethodId methodId,
        BinaryReader reader,
        Func<CancellationToken> beginMethodRunCallback);
}

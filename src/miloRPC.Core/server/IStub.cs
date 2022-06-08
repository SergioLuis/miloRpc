using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using miloRPC.Core.Shared;

namespace miloRPC.Core.Server;

public interface IStub
{
    bool CanHandleMethod(IMethodId method);

    IEnumerable<IMethodId> GetHandledMethods();

    Task<RpcNetworkMessages> RunMethodCallAsync(
        IMethodId methodId,
        BinaryReader reader,
        IConnectionContext connectionContext,
        Func<CancellationToken> beginMethodRunCallback);
}

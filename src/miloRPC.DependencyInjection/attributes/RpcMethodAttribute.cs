using System;

using miloRPC.Core.Shared;

namespace miloRPC.DependencyInjection.Attributes;

/// <summary>
/// The RpcMethodAttribute allows decorating RPC stub methods with the ID of
/// the RPC method they handle.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class RpcMethodAttribute<T> : Attribute where T : struct
{
    public RpcMethodAttribute(T methodIdentifier)
    {
        mMethodIdentifier = methodIdentifier;
    }

    public bool Handles(IMethodId<T> methodId)
        => methodId.Id.Equals(mMethodIdentifier);

    readonly T mMethodIdentifier;
}

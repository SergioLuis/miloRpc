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
    public T MethodIdentifier { get; }

    public RpcMethodAttribute(T methodIdentifier)
    {
        MethodIdentifier = methodIdentifier;
    }

    public bool Handles(IMethodId<T> methodId)
        => methodId.Id.Equals(MethodIdentifier);
}

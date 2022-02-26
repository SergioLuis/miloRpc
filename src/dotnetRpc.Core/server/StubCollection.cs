using System;
using System.Collections.Generic;
using System.Linq;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Server;

public class StubCollection
{
    internal IStub? FindStub(IMethodId methodId)
    {
        return mStubList.FirstOrDefault(s => s.CanHandleMethod(methodId));
    }

    public void RegisterStub(IStub stub)
    {
        foreach (IMethodId methodId in stub.GetHandledMethods())
        {
            if (mMethodNames.ContainsKey(methodId))
            {
                Environment.FailFast(
                    "The StubCollection already has a stub that handles"
                    + $" MethodId {methodId}. Execution is going to be"
                    + " halted immediately to prevent possible errors"
                    + " (including data loss), as there is an initialization"
                    + " error at the very core.");
            }

            mMethodNames.Add(methodId, methodId.Name);
        }

        mStubList.Add(stub);
    }

    internal string? SolveMethodName(IMethodId methodId)
    {
        if (mMethodNames.TryGetValue(methodId, out string? result))
            return result;

        return string.Empty;
    }

    readonly List<IStub> mStubList = new();
    readonly Dictionary<IMethodId, string?> mMethodNames = new();
}

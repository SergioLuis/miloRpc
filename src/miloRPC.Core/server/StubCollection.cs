using System;
using System.Collections.Generic;
using System.Linq;

using miloRPC.Core.Shared;

namespace miloRPC.Core.Server;

public class StubCollection
{
    public StubCollection() { }

    public StubCollection(params IStub[] stubs)
    {
        foreach (IStub stub in stubs)
            RegisterStub(stub);
    }

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
        => mMethodNames.TryGetValue(methodId, out string? result)
            ? result
            : string.Empty;

    readonly List<IStub> mStubList = new();
    readonly Dictionary<IMethodId, string?> mMethodNames = new();
}

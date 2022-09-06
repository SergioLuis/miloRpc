using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using miloRPC.Core.Server;
using miloRPC.Core.Shared;
using miloRPC.DependencyInjection.Attributes;

namespace miloRPC.DependencyInjection;

public class InvalidRpcMethodStubException : RpcException
{
    public InvalidRpcMethodStubException(MethodInfo methodInfo, string message)
        : base(string.Format(MessageTemplate, methodInfo.DeclaringType!.FullName, methodInfo.Name, message)) { }

    const string MessageTemplate = "Error initializing method stub for {0}.{1}: {2}";
}

public class DependencyInjectionStub<T1, T2> : IStub where T1 : IMethodId<T2> where T2 : struct
{
    public DependencyInjectionStub(
        IServiceCollection serviceCollection,
        Func<T2, string, T1> buildMethodId)
    {
        mServiceCollection = serviceCollection;
        mBuildMethodId = buildMethodId;
        mCachedMethods = new Dictionary<T2, MethodInfo>();

        mLog = RpcLoggerFactory.CreateLogger("DependencyInjection");
    }

    bool IStub.CanHandleMethod(IMethodId method)
        => method is IMethodId<T2> mi && mCachedMethods.ContainsKey(mi.Id);

    IEnumerable<IMethodId> IStub.GetHandledMethods()
    {
        const BindingFlags methodBindingFlags = BindingFlags.Instance | BindingFlags.Public; 

        Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (Assembly assembly in loadedAssemblies)
        {
            foreach (Type type in assembly.GetTypes())
            {
                foreach (MethodInfo methodInfo in type.GetMethods(methodBindingFlags))
                {
                    RpcMethodAttribute<T2>? attr =
                        methodInfo.GetCustomAttribute<RpcMethodAttribute<T2>>();

                    if (attr is null)
                        continue;

                    if (mCachedMethods.ContainsKey(attr.MethodIdentifier))
                    {
                        throw new InvalidRpcMethodStubException(
                            methodInfo,
                            $"Method with ID {attr.MethodIdentifier} is already registered!");
                    }

                    CheckMethodComplies(methodInfo);

                    T1 methodId = mBuildMethodId(
                        attr.MethodIdentifier,
                        GetFullyQualifiedName(methodInfo));

                    mCachedMethods.Add(attr.MethodIdentifier, methodInfo);

                    yield return methodId;
                }
            }
        }

        static string GetFullyQualifiedName(MethodInfo methodInfo)
            => $"{methodInfo.DeclaringType!.FullName}.{methodInfo.Name}";

        static void CheckMethodComplies(MethodInfo methodInfo)
        {
            bool containsFromDeserializedRequest = false;
            bool containsCancellationToken = false;

            if (!methodInfo.ReturnType.IsSubclassOf(typeof(INetworkMessage)))
            {
                if (!methodInfo.ReturnType.IsSubclassOf(typeof(Task<>)))
                    goto WRONG_RETURN_TYPE;

                Type[] genericArguments = methodInfo.ReturnType.GetGenericArguments();
                if (genericArguments.Length != 1)
                    goto WRONG_RETURN_TYPE;

                if (!genericArguments[0].IsSubclassOf(typeof(INetworkMessage)))
                    goto WRONG_RETURN_TYPE;

                goto CORRECT_RETURN_TYPE;

                WRONG_RETURN_TYPE:
                throw new InvalidRpcMethodStubException(
                    methodInfo,
                    $"Return type must be either {typeof(INetworkMessage).FullName} " +
                    $"or {typeof(Task<INetworkMessage>).FullName}");
            }

            CORRECT_RETURN_TYPE:

            ParameterInfo[] parameters = methodInfo.GetParameters();
            foreach (ParameterInfo parameter in parameters)
            {
                if (parameter.ParameterType == typeof(CancellationToken))
                {
                    if (containsCancellationToken)
                    {
                        throw new InvalidRpcMethodStubException(
                            methodInfo,
                            "There must not be more than one parameter of type " +
                            $"{typeof(CancellationToken).FullName}");
                    }

                    containsCancellationToken = true;
                    continue;
                }

                if (parameter.GetCustomAttribute<FromDeserializedRequestAttribute>() is not null)
                {
                    if (containsFromDeserializedRequest)
                    {
                        throw new InvalidRpcMethodStubException(
                            methodInfo,
                            "There must not be more than one parameter decorated with " +
                            $"{typeof(FromDeserializedRequestAttribute).FullName} attribute");
                    }

                    if (!parameter.ParameterType.IsSubclassOf(typeof(INetworkMessage)))
                    {
                        throw new InvalidRpcMethodStubException(
                            methodInfo,
                            $"Parameter decorated with attribute " +
                            $"{typeof(FromDeserializedRequestAttribute).FullName} " +
                            $"must be a subclass of ${typeof(INetworkMessage).FullName}");
                    }

                    containsFromDeserializedRequest = true;
                    continue;
                }

                if (parameter.GetCustomAttribute<FromServicesAttribute>() is null)
                {
                    throw new InvalidRpcMethodStubException(
                        methodInfo,
                        $"Parameters must be decorated either with " +
                        $"{typeof(FromDeserializedRequestAttribute).FullName} " +
                        $"or {typeof(FromServicesAttribute).FullName}");
                }
            }

            if (!containsCancellationToken)
            {
                throw new InvalidRpcMethodStubException(
                    methodInfo,
                    $"One parameter must be of type {typeof(CancellationToken).FullName}");
            }

            if (!containsFromDeserializedRequest)
            {
                throw new InvalidRpcMethodStubException(
                    methodInfo,
                    $"One parameter must be decorated with " +
                    $"{typeof(FromDeserializedRequestAttribute).FullName}");
            }
        }
    }

    async Task<RpcNetworkMessages> IStub.RunMethodCallAsync(
        IMethodId methodId,
        BinaryReader reader,
        IConnectionContext connectionContext,
        Func<CancellationToken> beginMethodRunCallback)
    {
        throw new NotImplementedException();
    }

    readonly IServiceCollection mServiceCollection;
    readonly Func<T2, string, T1> mBuildMethodId;
    readonly Dictionary<T2, MethodInfo> mCachedMethods;

    readonly ILogger mLog;
}

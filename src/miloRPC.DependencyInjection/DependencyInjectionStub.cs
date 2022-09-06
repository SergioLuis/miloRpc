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
    {
        throw new NotImplementedException();
    }

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
                        throw new InvalidOperationException(
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

            ParameterInfo[] parameters = methodInfo.GetParameters();
            foreach (ParameterInfo parameter in parameters)
            {
                if (parameter.ParameterType == typeof(CancellationToken))
                {
                    if (containsCancellationToken)
                    {
                        // Duplicated!!
                        throw new InvalidOperationException();
                    }

                    containsCancellationToken = true;
                    continue;
                }

                if (parameter.GetCustomAttribute<FromDeserializedRequestAttribute>() is not null)
                {
                    if (containsFromDeserializedRequest)
                    {
                        // Duplicated!!
                        throw new InvalidOperationException("LOCALIZE MESSAGE");
                    }

                    containsFromDeserializedRequest = true;
                    continue;
                }

                if (parameter.GetCustomAttribute<FromServicesAttribute>() is null)
                {
                    // Contains non-decorated parameters!!
                    throw new InvalidOperationException("LOCALIZE MESSAGE");
                }
            }

            if (!containsCancellationToken)
            {
                // No cancellation token!
                throw new InvalidOperationException("LOCALIZE MESSAGE");
            }

            if (!containsFromDeserializedRequest)
            {
                // No parameter from deserialized request!!
                throw new InvalidOperationException("LOCALIZE MESSAGE");
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

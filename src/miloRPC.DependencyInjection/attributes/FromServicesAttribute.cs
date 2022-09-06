using System;

namespace miloRPC.DependencyInjection.Attributes;

/// <summary>
/// The FromServicesAttribute enables injecting a service directly into a
/// RPC method stub without using constructor injection.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class FromServicesAttribute : Attribute { }

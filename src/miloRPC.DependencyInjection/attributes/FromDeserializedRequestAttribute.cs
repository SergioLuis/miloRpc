using System;

namespace miloRPC.DependencyInjection.Attributes;

/// <summary>
/// The FromDeserializedRequestAttribute enables receiving the deserialized
/// request directly into the RPC method stub without manually having to
/// handle message deserialization.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class FromDeserializedRequestAttribute : Attribute { }

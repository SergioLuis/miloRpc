using System;

namespace miloRPC.Core.Shared;

[Flags]
public enum RpcCapabilities : byte
{
    None = 0,
    Ssl = 1 << 0,
    Compression = 1 << 1
}

public static class GetRpcCapabilitiesFromSettings
{
    public static RpcCapabilities GetMandatory(ConnectionSettings settings)
    {
        RpcCapabilities result = RpcCapabilities.None;
        if (settings.Ssl.Status is SharedCapabilityEnablement.EnabledMandatory)
            result |= RpcCapabilities.Ssl;

        if (settings.Compression.Status is SharedCapabilityEnablement.EnabledMandatory)
            result |= RpcCapabilities.Compression;

        return result;
    }

    public static RpcCapabilities GetOptional(ConnectionSettings settings)
    {
        RpcCapabilities result = RpcCapabilities.None;
        if (settings.Ssl.Status is SharedCapabilityEnablement.EnabledOptional)
            result |= RpcCapabilities.Ssl;

        if (settings.Compression.Status is SharedCapabilityEnablement.EnabledOptional)
            result |= RpcCapabilities.Compression;

        return result;
    }
}

public class RpcCapabilitiesNegotiationResult
{
    public bool NegotiatedOk => RequiredMissingCapabilities == RpcCapabilities.None;
    public RpcCapabilities CommonCapabilities { get; }
    public RpcCapabilities OptionalMissingCapabilities { get; }
    public RpcCapabilities RequiredMissingCapabilities { get; }

    private RpcCapabilitiesNegotiationResult(
        RpcCapabilities common,
        RpcCapabilities optionalMissing,
        RpcCapabilities requiredMissing)
    {
        CommonCapabilities = common;
        OptionalMissingCapabilities = optionalMissing;
        RequiredMissingCapabilities = requiredMissing;
    }

    public static RpcCapabilitiesNegotiationResult Build(
        RpcCapabilities mandatorySelf,
        RpcCapabilities optionalSelf,
        RpcCapabilities mandatoryOther,
        RpcCapabilities optionalOther)
    {
        RpcCapabilities selfAll = mandatorySelf | optionalSelf;
        RpcCapabilities otherAll = mandatoryOther | optionalOther;

        RpcCapabilities common = selfAll & otherAll;
        RpcCapabilities selfOptionalMissing =
            (selfAll ^ optionalOther) & optionalOther;
        RpcCapabilities selfMandatoryMissing =
            (selfAll ^ mandatoryOther) & mandatoryOther;

        RpcCapabilities otherOptionalMissing =
            (otherAll ^ optionalSelf) & optionalSelf;
        RpcCapabilities otherMandatoryMissing =
            (otherAll ^ mandatorySelf) & mandatorySelf;

        RpcCapabilities optionalMissing =
            selfOptionalMissing | otherOptionalMissing;
        RpcCapabilities mandatoryMissing =
            selfMandatoryMissing | otherMandatoryMissing;

        return new RpcCapabilitiesNegotiationResult(common, optionalMissing, mandatoryMissing);
    }
}

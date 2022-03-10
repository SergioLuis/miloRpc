using System;

namespace dotnetRpc.Core.Shared;

[Flags]
public enum RpcCapabilities : byte
{
    None = 0,
    Ssl = 1 << 0,
    Compression = 1 << 1
}

internal class RpcCapabilitiesNegotiationResult
{
    internal bool NegotiatedOk => RequiredMissingCapabilities == RpcCapabilities.None;
    internal RpcCapabilities CommonCapabilities { get; private set; }
    internal RpcCapabilities OptionalMissingCapabilities { get; private set; }
    internal RpcCapabilities RequiredMissingCapabilities { get; private set; }

    private RpcCapabilitiesNegotiationResult(
        RpcCapabilities common,
        RpcCapabilities optionalMissing,
        RpcCapabilities requiredMissing)
    {
        CommonCapabilities = common;
        OptionalMissingCapabilities = optionalMissing;
        RequiredMissingCapabilities = requiredMissing;
    }

    internal static RpcCapabilitiesNegotiationResult Build(
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

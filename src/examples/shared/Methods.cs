using miloRPC.Core.Shared;

namespace miloRPC.Examples.Shared;

public static class MethodId
{
    public const byte Echo = 1;
}

public static class Methods
{
    public static readonly IMethodId Echo = new DefaultMethodId(MethodId.Echo, "Echo");
}

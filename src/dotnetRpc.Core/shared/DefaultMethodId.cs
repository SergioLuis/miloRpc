namespace dotnetRpc.Core.Shared;

public class DefaultMethodId : IMethodId
{
    public string Name => mName;
    public byte Id => mId;

    public DefaultMethodId(byte id)
    {
        mId = id;
        mName = string.Empty;
    }

    public DefaultMethodId(byte id, string name)
    {
        mId = id;
        mName = name;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;

        if (obj is not DefaultMethodId otherMethodId)
            return false;

        return mId == otherMethodId.Id;
    }

    public override int GetHashCode() => mId.GetHashCode();

    public override string ToString() => string.IsNullOrEmpty(mName)
        ? $"[0x{mId:X} (NO_NAME)]"
        : $"[0x{mId:X} ({mName})]";

    void IMethodId.SetSolvedMethodName(string? name)
        => mName = name ?? string.Empty;

    string mName;
    readonly byte mId;
}

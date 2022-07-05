namespace miloRPC.Core.Shared;

public class DefaultMethodId : IMethodId
{
    public string Name { get; private set; }

    public byte Id { get; }

    public DefaultMethodId(byte id)
    {
        Id = id;
        Name = string.Empty;
    }

    public DefaultMethodId(byte id, string name)
    {
        Id = id;
        Name = name;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;

        if (obj is not DefaultMethodId otherMethodId)
            return false;

        return Id == otherMethodId.Id;
    }

    public static bool operator ==(DefaultMethodId a, DefaultMethodId b)
        => a.Id == b.Id;

    public static bool operator !=(DefaultMethodId a, DefaultMethodId b)
        => a.Id != b.Id;

    public static bool operator <(DefaultMethodId a, DefaultMethodId b)
        => a.Id < b.Id;

    public static bool operator <=(DefaultMethodId a, DefaultMethodId b)
        => a.Id <= b.Id;

    public static bool operator >(DefaultMethodId a, DefaultMethodId b)
        => a.Id > b.Id;

    public static bool operator >=(DefaultMethodId a, DefaultMethodId b)
        => a.Id >= b.Id;

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => string.IsNullOrEmpty(Name)
        ? $"[0x{Id:X} (NO_NAME)]"
        : $"[0x{Id:X} ({Name})]";

    void IMethodId.SetSolvedMethodName(string? name) => Name = name ?? string.Empty;
}

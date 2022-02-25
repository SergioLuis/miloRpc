using System.IO;

using dotnetRpc.Shared;

namespace dotnetRpc.Server;

public interface IReadMethodId
{
    IMethodId ReadMethodId(BinaryReader reader);
}

public class DefaultReadMethodId : IReadMethodId
{
    IMethodId IReadMethodId.ReadMethodId(BinaryReader reader)
        => new DefaultMethodId(reader.ReadByte());

    public static readonly IReadMethodId Instance = new DefaultReadMethodId();
}

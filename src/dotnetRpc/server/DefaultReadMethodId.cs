using System.IO;

using dotnetRpc.Shared;

namespace dotnetRpc.Server;

public class DefaultReadMethodId : IReadMethodId
{
    IMethodId IReadMethodId.ReadMethodId(BinaryReader reader)
        => new DefaultMethodId(reader.ReadByte());
}

using System.IO;

using miloRPC.Core.Shared;

namespace miloRPC.Core.Server;

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

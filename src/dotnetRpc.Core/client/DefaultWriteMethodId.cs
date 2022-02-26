using System.IO;
using System.Runtime.CompilerServices;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Core.Client;

public interface IWriteMethodId
{
    void WriteMethodId(BinaryWriter writer, IMethodId methodId);
}

public class DefaultWriteMethodId : IWriteMethodId
{
    void IWriteMethodId.WriteMethodId(BinaryWriter writer, IMethodId methodId)
        => writer.Write((byte)Unsafe.As<DefaultMethodId>(methodId).Id);

    public static readonly IWriteMethodId Instance = new DefaultWriteMethodId();
}

using System.IO;
using System.Runtime.CompilerServices;

using dotnetRpc.Shared;

namespace dotnetRpc.Client;

public class DefaultWriteMethodId : IWriteMethodId
{
    void IWriteMethodId.WriteMethodId(BinaryWriter writer, IMethodId methodId)
        => writer.Write((byte)Unsafe.As<DefaultMethodId>(methodId).Id);
}

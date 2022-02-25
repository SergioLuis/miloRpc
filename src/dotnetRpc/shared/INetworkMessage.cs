using System.IO;

namespace dotnetRpc.Shared;

public interface INetworkMessage
{
    void Serialize(BinaryWriter writer);
    void Deserialize(BinaryReader reader);
}

public record RpcNetworkMessages(INetworkMessage Request, INetworkMessage Response);

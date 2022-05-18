using System.Threading;
using System.Threading.Tasks;

namespace miloRPC.Core.Client;

public interface IConnectToServer
{
    Task<ConnectionToServer> ConnectAsync(CancellationToken ct);
}

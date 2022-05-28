using System;
using System.Threading;
using System.Threading.Tasks;

namespace miloRPC.Examples.Shared;

public class EchoResult
{
    public DateTime ReceptionDateUtc { get; init; }
    public string ReceivedMessage { get; init; }

    public EchoResult(DateTime receptionDateUtc, string receivedMessage)
    {
        ReceptionDateUtc = receptionDateUtc;
        ReceivedMessage = receivedMessage;
    }
}

public interface IEchoService
{
    Task<EchoResult> EchoAsync(string message, CancellationToken ct);
}

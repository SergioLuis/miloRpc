using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace miloRPC.Core.Server;

public class AcceptLoopStartEventArgs
{
    public int LaunchCount { get; }
    public bool CancelRequested { get; set; }

    public AcceptLoopStartEventArgs(int launchCount)
    {
        LaunchCount = launchCount;
        CancelRequested = false;
    }
}

public class AcceptLoopStopEventArgs
{
    public int LaunchCount { get; }

    public AcceptLoopStopEventArgs(int launchCount)
    {
        LaunchCount = launchCount;
    }
}

public class ConnectionAcceptEventArgs<T>
{
    public T? EndPoint { get; }
    public bool CancelRequested { get; set; }

    public ConnectionAcceptEventArgs(T? endPoint)
    {
        EndPoint = endPoint;
    }
}

public interface IServer<T>
{
    // ReSharper disable once EventNeverSubscribedTo.Global
    event EventHandler<AcceptLoopStartEventArgs> AcceptLoopStart;

    // ReSharper disable once EventNeverSubscribedTo.Global
    event EventHandler<AcceptLoopStopEventArgs> AcceptLoopStop;

    // ReSharper disable once EventNeverSubscribedTo.Global
    event EventHandler<ConnectionAcceptEventArgs<T>> ConnectionAccept;

    string ServerProtocol { get; }
    T? BindAddress { get; }
    ActiveConnections ActiveConnections { get; }
    ConnectionTimeouts ConnectionTimeouts { get; }
    Task ListenAsync(CancellationToken ct);
}

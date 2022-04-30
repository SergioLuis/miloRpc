using System;
using System.Threading;

namespace miloRPC.Core.Server;

public class ConnectionTimeouts
{
    public TimeSpan Idling
    {
        get { lock (mIdlingLock) return mIdling; }
        set { lock (mIdlingLock) mIdling = value; }
    }

    public TimeSpan Running
    {
        get { lock (mRunningLock) return mRunning; }
        set { lock (mRunningLock) mRunning = value; }
    }

    public ConnectionTimeouts(TimeSpan idling, TimeSpan running)
    {
        mIdling = idling;
        mRunning = running;
    }

    TimeSpan mIdling;
    TimeSpan mRunning;

    readonly object mIdlingLock = new();
    readonly object mRunningLock = new();

    public static readonly ConnectionTimeouts AllInfinite =
        new(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
}

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

    public TimeSpan ProcessingEndOfDataSequence
    {
        get { lock (mProcessingEodLock) return mProcessingEndOfDataSequence; }
        set { lock (mProcessingEodLock) mProcessingEndOfDataSequence = value; }
    }

    public ConnectionTimeouts(
        TimeSpan idling, TimeSpan running, TimeSpan processingEndOfDataSequence)
    {
        mIdling = idling;
        mRunning = running;
        mProcessingEndOfDataSequence = processingEndOfDataSequence;
    }

    TimeSpan mIdling;
    TimeSpan mRunning;
    TimeSpan mProcessingEndOfDataSequence;

    readonly object mIdlingLock = new();
    readonly object mRunningLock = new();
    readonly object mProcessingEodLock = new();

    public static readonly ConnectionTimeouts Default = new(
        idling: Timeout.InfiniteTimeSpan,
        running: Timeout.InfiniteTimeSpan,
        processingEndOfDataSequence: TimeSpan.FromSeconds(30));
}

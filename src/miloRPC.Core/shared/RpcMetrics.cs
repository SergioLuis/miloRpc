namespace miloRPC.Core.Shared;

public class RpcMetrics
{
    public struct RpcCounters
    {
        public uint TotalConnections;
        public uint ActiveConnections;
        public uint TotalMethodCalls;
        public uint ActiveMethodCalls;

        public ulong TotalReceivedBytes;
        public ulong TotalSentBytes;
    }

    internal RpcCounters Counters { get { lock (mSyncLock) return mCounters; } }

    internal uint ConnectionStart()
    {
        lock (mSyncLock)
        {
            mCounters.TotalConnections++;
            mCounters.ActiveConnections++;

            return mCounters.TotalConnections;
        }
    }

    internal void ConnectionEnd()
    {
        lock (mSyncLock)
        {
            mCounters.ActiveConnections--;
        }
    }

    internal uint MethodCallStart()
    {
        lock (mSyncLock)
        {
            mCounters.TotalMethodCalls++;
            mCounters.ActiveMethodCalls++;

            return mCounters.TotalMethodCalls;
        }
    }

    internal void MethodCallEnd(ulong callReadBytes, ulong callWrittenBytes)
    {
        lock (mSyncLock)
        {
            mCounters.ActiveMethodCalls--;
            mCounters.TotalReceivedBytes += callReadBytes;
            mCounters.TotalSentBytes += callWrittenBytes;
        }
    }

    RpcCounters mCounters = new();
    readonly object mSyncLock = new object();
}

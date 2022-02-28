namespace dotnetRpc.Core.Shared;

public class RpcMetrics
{
    public struct RpcCounters
    {
        public uint TotalConnections;
        public uint ActiveConnections;
        public uint TotalMethodCalls;
        public uint ActiveMethodCalls;
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

    internal void MethodCallEnd()
    {
        lock (mSyncLock)
        {
            mCounters.ActiveMethodCalls--;
        }
    }

    RpcCounters mCounters = new();
    static readonly object mSyncLock = new object();
}

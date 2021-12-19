namespace dotnetRpc.Shared;

public class RpcMetrics
{
    public struct RpcCounters
    {
        public uint TotalConnections = 0;
        public uint ActiveConnections = 0;
        public uint TotalMethodCalls = 0;
        public uint ActiveMethodCalls = 0;
    }

    public RpcCounters Counters => mCounters;

    public uint ConnectionStart()
    {
        lock (mSyncLock)
        {
            mCounters.TotalConnections++;
            mCounters.ActiveConnections++;

            return mCounters.TotalConnections;
        }
    }

    public void ConnectionEnd()
    {
        lock (mSyncLock)
        {
            mCounters.ActiveConnections--;
        }
    }

    public uint MethodCallStart()
    {
        lock (mSyncLock)
        {
            mCounters.TotalMethodCalls++;
            mCounters.ActiveMethodCalls++;

            return mCounters.TotalMethodCalls;
        }
    }

    public void MethodCallEnd()
    {
        lock (mSyncLock)
        {
            mCounters.ActiveMethodCalls--;
        }
    }

    RpcCounters mCounters = new();
    static readonly object mSyncLock = new object();
}

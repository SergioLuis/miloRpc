using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

using miloRPC.Core.Shared;

namespace miloRPC.Core.Client;

public class ConnectionPool
{
    public int PooledConnections
    {
        get { lock (mRentLock) return mPooledConnections.Count; }
    }

    public int RentedConnections
    {
        get { lock (mRentLock) return mRentedConnections.Count; }
    }

    public int WaitingThreads
    {
        get { lock (mRentLock) return mWaitingThreads; }
    }

    public ConnectionPool(
        IConnectToServer connectToServer,
        int minimumPooledConnections = 2)
    {
        mConnectToServer = connectToServer;
        mMinimumPooledConnections = minimumPooledConnections;
        mPooledConnections = new Queue<ConnectionToServer>(minimumPooledConnections * 2);

        mLog = RpcLoggerFactory.CreateLogger("ConnectionPool");
    }

    public async ValueTask WarmupPool()
    {
        await mCreationLock.WaitAsync();
        try
        {
            for (int i = 0; i < mMinimumPooledConnections; i++)
            {
                mPooledConnections.Enqueue(
                    await mConnectToServer.ConnectAsync(CancellationToken.None));
            }
        }
        finally
        {
            mCreationLock.Release();
        }
    }

    public async Task<ConnectionToServer> RentConnectionAsync(CancellationToken ct = default)
        => await RentConnectionAsync(Timeout.InfiniteTimeSpan, ct);

    public async Task<ConnectionToServer> RentConnectionAsync(
        TimeSpan waitTimeout, CancellationToken ct = default)
    {
        int reqIni = Environment.TickCount;
        ConnectionToServer? result = null;

        bool rentLockTaken = false;
        Monitor.Enter(mRentLock, ref rentLockTaken);
        try
        {
            result = DequeueNextValidConnection(mPooledConnections);
            if (result is not null)
            {
                mRentedConnections.Add(result.ConnectionId);
                mLog.LogTrace(
                    "Satisfied request in {SatisfyMs} ms (result was pooled)",
                    Environment.TickCount - reqIni);
                return result;
            }

            mWaitingThreads++;

            // There are no valid connections pooled but the caller is willing
            // to wait for a rented connection to be returned
            if (mRentedConnections.Count > 0 && waitTimeout != TimeSpan.Zero)
            {
                bool yielded = false;

                // Don't try to yield if the wait is going to be minimum
                // Most probably the overhead will not compensate the yielding
                if (waitTimeout == Timeout.InfiniteTimeSpan
                    || waitTimeout >= TimeSpan.FromMilliseconds(150))
                {
                    Monitor.Exit(mRentLock);
                    rentLockTaken = false;

                    yielded = true;

                    // Yield, as the wait can take some time
                    await Task.Yield();

                    Monitor.Enter(mRentLock, ref rentLockTaken);
                }

                try
                {
                    if (yielded)
                    {
                        // We need to check again if there are pooled connections,
                        // as we might have missed a PulseAll on 'mRentLock' while
                        // we gave up the lock for switching thread context
                        result = DequeueNextValidConnection(mPooledConnections);
                        if (result is not null)
                        {
                            mWaitingThreads--;
                            mRentedConnections.Add(result.ConnectionId);
                            mLog.LogTrace(
                                "Satisfied request in {SatisfyMs} ms (result was pooled after some time)",
                                Environment.TickCount - reqIni);
                            return result;
                        }
                    }

                    if (Monitor.Wait(mRentLock, waitTimeout))
                    {
                        result = DequeueNextValidConnection(mPooledConnections);
                        if (result is not null)
                        {
                            mWaitingThreads--;
                            mRentedConnections.Add(result.ConnectionId);
                            mLog.LogTrace(
                                "Satisfied request in {SatisfyMs} ms (result was pooled after some time)",
                                Environment.TickCount - reqIni);
                            return result;
                        }
                    }
                }
                finally
                {
                    if (rentLockTaken)
                    {
                        Monitor.Exit(mRentLock);
                        rentLockTaken = false;
                    }
                }
            }
        }
        finally
        {
            if (rentLockTaken)
            {
                Monitor.Exit(mRentLock);
                rentLockTaken = false;
            }
        }

        await mCreationLock.WaitAsync(ct);
        try
        {
            int waitingThreads;
            Monitor.Enter(mRentLock);
            try
            {
                mWaitingThreads--;
                if (mWaitingThreads < 0) mWaitingThreads = 0;

                result = DequeueNextValidConnection(mPooledConnections);
                if (result is not null)
                {
                    mRentedConnections.Add(result.ConnectionId);
                    mLog.LogTrace(
                        "Satisfied request in {SatisfyMs} ms (result was pooled after some time)",
                        Environment.TickCount - reqIni);
                    return result;
                }

                waitingThreads = mWaitingThreads;
            }
            finally
            {
                Monitor.Exit(mRentLock);
            }

            int connectionsToCreate = waitingThreads
                + mMinimumPooledConnections
                + 1;

            List<ConnectionToServer> newConnections = new(connectionsToCreate);
            int ini = Environment.TickCount;

            for (int i = 0; i < connectionsToCreate; i++)
            {
                ct.ThrowIfCancellationRequested();
                newConnections.Add(await mConnectToServer.ConnectAsync(ct));
            }

            mLog.LogInformation(
                "Created {NewConnNumber} new connections in {NewConnMs} ms",
                connectionsToCreate, Environment.TickCount - ini);

            Monitor.Enter(mRentLock);
            try
            {
                for (int i = 0; i < connectionsToCreate - 1; i++)
                    mPooledConnections.Enqueue(newConnections[i]);

                result = newConnections[connectionsToCreate - 1];
                mRentedConnections.Add(result.ConnectionId);

                Monitor.PulseAll(mRentLock);
            }
            finally
            {
                Monitor.Exit(mRentLock);
            }

            mLog.LogTrace(
                "Satisfied request in {SatisfyMs} ms (result was created by this req)",
                Environment.TickCount - reqIni);
            return result;
        }
        finally
        {
            mCreationLock.Release();
        }

        static ConnectionToServer? DequeueNextValidConnection(
            Queue<ConnectionToServer> queue)
        {
            bool success = false;
            ConnectionToServer? result = null;

            while (!success && queue.Count > 0)
            {
                result = queue.Dequeue();
                success = result.IsConnected();
            }

            return success ? result : null;
        }
    }

    public void ReturnConnection(ConnectionToServer connection)
    {
        Monitor.Enter(mRentLock);
        try
        {
            if (!mRentedConnections.Contains(connection.ConnectionId))
            {
                connection.Dispose();
                return;
            }

            mRentedConnections.Remove(connection.ConnectionId);
            if (!connection.IsConnected())
            {
                connection.Dispose();
                return;
            }

            mPooledConnections.Enqueue(connection);
            Monitor.PulseAll(mRentLock);
        }
        finally
        {
            Monitor.Exit(mRentLock);
        }
    }

    int mWaitingThreads = 0;

    readonly IConnectToServer mConnectToServer;
    readonly int mMinimumPooledConnections;
    readonly Queue<ConnectionToServer> mPooledConnections;
    readonly HashSet<uint> mRentedConnections = new();

    readonly object mRentLock = new();
    readonly SemaphoreSlim mCreationLock = new(1);

    readonly ILogger mLog;
}

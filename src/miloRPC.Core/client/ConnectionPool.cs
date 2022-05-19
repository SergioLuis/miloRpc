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

    public async Task<ConnectionToServer> RentConnectionAsync()
        => await RentConnectionAsync(Timeout.InfiniteTimeSpan, CancellationToken.None);

    public async Task<ConnectionToServer> RentConnectionAsync(CancellationToken ct)
        => await RentConnectionAsync(Timeout.InfiniteTimeSpan, ct);

    public async Task<ConnectionToServer> RentConnectionAsync(TimeSpan waitTimeout)
        => await RentConnectionAsync(waitTimeout, CancellationToken.None);

    public async Task<ConnectionToServer> RentConnectionAsync(
        TimeSpan waitTimeout, CancellationToken ct)
    {
        ConnectionToServer? result = null;

        bool rentLockTaken = false;
        Monitor.Enter(mRentLock, ref rentLockTaken);
        try
        {
            result = DequeueNextValidConnection(mPooledConnections);
            if (result is not null)
            {
                mRentedConnections.Add(result.ConnectionId);
                return result;
            }

            // There are no valid connections pooled and the caller refuses to wait
            // for a rented connection to be returned.
            // Just create the necessary connections and be done with it
            if (waitTimeout == TimeSpan.Zero)
            {
                Monitor.Exit(mRentLock);
                rentLockTaken = false;

                // Only one thread is allowed to create connections at the same time
                await mCreationLock.WaitAsync(ct);
                try
                {
                    int connectionsToCreate = -1;

                    // We need to check again whether or not there are new connections pooled,
                    // in case there was another thread creating them while we awaited
                    // for the 'mCreationLock'
                    Monitor.Enter(mRentLock, ref rentLockTaken);
                    try
                    {
                        result = DequeueNextValidConnection(mPooledConnections);
                        if (result is not null)
                        {
                            mRentedConnections.Add(result.ConnectionId);
                            return result;
                        }

                        connectionsToCreate = Math.Max(
                            mWaitingThreads * 2,
                            mMinimumPooledConnections) + 1;
                    }
                    finally
                    {
                        if (rentLockTaken)
                        {
                            Monitor.Exit(mRentLock);
                            rentLockTaken = false;
                        }
                    }

                    // There are definitely no pooled connections - we proceed to create them
                    List<ConnectionToServer> newConnections = new(connectionsToCreate);
                    for (int i = 0; i < connectionsToCreate; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        newConnections.Add(await mConnectToServer.ConnectAsync(ct));
                    }

                    Monitor.Enter(mRentLock, ref rentLockTaken);
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
                        if (rentLockTaken)
                        {
                            Monitor.Exit(mRentLock);
                            rentLockTaken = false;
                        }
                    }
                }
                finally
                {
                    mCreationLock.Release();
                }

                return result;
            }

            // There are no valid connections pooled but the caller is willing
            // to wait for a rented connection to be returned
            if (mRentedConnections.Count > 0)
            {
                mWaitingThreads++;

                if (Monitor.Wait(mRentLock, waitTimeout))
                {
                    result = DequeueNextValidConnection(mPooledConnections);
                    if (result is not null)
                    {
                        mWaitingThreads--;
                        mRentedConnections.Add(result.ConnectionId);
                        return result;
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
            lock (mRentLock)
            {
                result = DequeueNextValidConnection(mPooledConnections);
                if (result is not null)
                {
                    mWaitingThreads--;
                    if (mWaitingThreads < 0) mWaitingThreads = 0;

                    mRentedConnections.Add(result.ConnectionId);
                    return result;
                }

                waitingThreads = mWaitingThreads;
            }

            int connectionsToCreate = Math.Max(
                waitingThreads * 2,
                mMinimumPooledConnections) + 1;

            List<ConnectionToServer> newConnections = new(connectionsToCreate);
            for (int i = 0; i < connectionsToCreate; i++)
            {
                ct.ThrowIfCancellationRequested();
                newConnections.Add(await mConnectToServer.ConnectAsync(ct));
            }

            lock (mRentLock)
            {
                for (int i = 0; i < connectionsToCreate - 1; i++)
                    mPooledConnections.Enqueue(newConnections[i]);

                result = newConnections[connectionsToCreate - 1];
                mRentedConnections.Add(result.ConnectionId);

                Monitor.PulseAll(mRentLock);
            }

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
        lock (mRentLock)
        {
            if (!mRentedConnections.Contains(connection.ConnectionId))
                return;

            mRentedConnections.Remove(connection.ConnectionId);
            if (!connection.IsConnected())
                return;

            mPooledConnections.Enqueue(connection);

            Monitor.PulseAll(mRentLock);
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

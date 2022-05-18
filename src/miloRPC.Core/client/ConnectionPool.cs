using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using miloRPC.Core.Shared;

namespace miloRPC.Core.Client;

public class ConnectionPool
{
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
        lock (mRentLock)
        {
            result = DequeueNextValidConnection(mPooledConnections);
            if (result is not null)
            {
                mRentedConnections.Add(result.ConnectionId);
                return result;
            }

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

                Monitor.PulseAll(mRentLock);
            }

            return newConnections[connectionsToCreate - 1];
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

            return result;
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

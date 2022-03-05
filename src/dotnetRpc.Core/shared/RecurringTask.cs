using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace dotnetRpc.Core.Shared;

public class RecurringTask
{
    public bool IsRunning => mIsRunning;
    public uint TimesInvoked => mTimesInvoked;
    public Exception? LastException { get { lock (mSyncLock) return mLastException; } }

    public RecurringTask(Func<CancellationToken, Task> action, string taskName)
    {
        mAction = action;
        mTaskName = taskName;

        mLog = RpcLoggerFactory.CreateLogger("RecurringTask");
    }

    public void Start(TimeSpan interval, CancellationToken ct)
    {
        mSemaphore.Wait();
        try
        {
            if (ct.IsCancellationRequested)
                return;

            if (mRecurringLoopCts != null)
                return;

            mOriginalRunInterval = interval;
            mOriginalStartToken = ct;
            mRecurringLoopCts = CancellationTokenSource.CreateLinkedTokenSource(mOriginalStartToken);
            mRecurringLoopTask = RunRecurringAsync(mAction, mOriginalRunInterval, ct, mRecurringLoopCts.Token);
        }
        finally
        {
            mSemaphore.Release();
        }
    }

    public async Task StopAsync()
        => await TryStopAsync(Timeout.InfiniteTimeSpan, CancellationToken.None);

    public async Task<bool> TryStopAsync(
        TimeSpan timeout, CancellationToken ct)
    {
        bool semaphoreEntered = await mSemaphore.WaitAsync(timeout, ct);
        if (!semaphoreEntered)
            return false;

        try
        {
            if (mRecurringLoopTask == null || mRecurringLoopTask.IsCompleted)
                return true;

            if (mRecurringLoopCts == null)
                return true;

            mRecurringLoopCts.Cancel();
            try
            {
                await mRecurringLoopTask;
                return true;
            }
            finally
            {
                mRecurringLoopCts.Dispose();
                mRecurringLoopCts = null;
            }
        }
        finally
        {
            mSemaphore.Release();
        }
    }

    public async Task FireEarlyAsync()
        => await TryFireEarlyAsync(Timeout.InfiniteTimeSpan, CancellationToken.None);

    public async Task<bool> TryFireEarlyAsync(
        TimeSpan timeout, CancellationToken ct)
    {
        bool semaphoreEntered = await mSemaphore.WaitAsync(timeout, ct);
        if (!semaphoreEntered)
            return false;

        try
        {
            if (mRecurringLoopCts != null)
                mRecurringLoopCts.Cancel();

            try
            {
                if (mRecurringLoopTask != null)
                    await mRecurringLoopTask;
            }
            finally
            {
                mRecurringLoopCts?.Dispose();
                mRecurringLoopCts = null;
                mRecurringLoopTask = null;
            }

            await mAction(ct);

            mRecurringLoopCts = CancellationTokenSource.CreateLinkedTokenSource(mOriginalStartToken);
            mRecurringLoopTask = RunRecurringAsync(mAction, mOriginalRunInterval, ct, mRecurringLoopCts.Token);
            return true;
        }
        finally
        {
            mSemaphore.Release();
        }
    }

    Task RunRecurringAsync(
        Func<CancellationToken, Task> action,
        TimeSpan interval,
        CancellationToken actionToken,
        CancellationToken breakToken)
    {
        return Task.Factory.StartNew(async () =>
        {
            mIsRunning = true;
            try
            {
                while (!breakToken.IsCancellationRequested)
                {
                    try
                    {
                        await action(actionToken);
                    }
                    catch (Exception ex)
                    {
                        lock (mSyncLock) mLastException = ex;
                    }

                    mTimesInvoked++;

                    await SafeDelay(interval, breakToken);
                }
            }
            finally
            {
                mIsRunning = false;
            }
        }, TaskCreationOptions.LongRunning).Unwrap();
    }

    static async Task SafeDelay(TimeSpan interval, CancellationToken ct)
    {
        try
        {
            await Task.Delay(interval, ct);
        }
        catch { }
    }

    Task? mRecurringLoopTask;
    TimeSpan mOriginalRunInterval;
    CancellationToken mOriginalStartToken;
    CancellationTokenSource? mRecurringLoopCts;
    Exception? mLastException;
    volatile bool mIsRunning;
    volatile uint mTimesInvoked;

    readonly Func<CancellationToken, Task> mAction;
    readonly string mTaskName;
    readonly object mSyncLock = new();
    readonly SemaphoreSlim mSemaphore = new(1, 1);

    readonly ILogger mLog;
}

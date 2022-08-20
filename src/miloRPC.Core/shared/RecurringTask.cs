using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace miloRPC.Core.Shared;

public class RecurringTask
{
    public bool IsRunning => mIsRunning;
    public uint TimesInvoked => mTimesInvoked;
    public Exception? LastException { get { lock (mSyncLock) return mLastException; } }

    public RecurringTask(Action<CancellationToken> action, string taskName)
    {
        mAction = action;
        mTaskName = taskName;

        mLog = RpcLoggerFactory.CreateLogger("RecurringTask");
    }

    public void Start(TimeSpan interval, CancellationToken ct)
    {
        mSemaphore.Wait(ct);
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
            mRecurringLoopCts?.Cancel();

            try
            {
                if (mRecurringLoopTask is not null)
                    await mRecurringLoopTask;
            }
            finally
            {
                mRecurringLoopCts?.Dispose();
                mRecurringLoopCts = null;
                mRecurringLoopTask = null;
            }

            mAction(ct);

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
        Action<CancellationToken> action,
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
                        mLog.LogTrace("Running recurring action {TaskName}", mTaskName);
                        action(actionToken);
                    }
                    catch (Exception ex)
                    {
                        lock (mSyncLock) mLastException = ex;
                    }

                    Interlocked.Increment(ref mTimesInvoked);

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
        catch
        {
            // ignored
        }
    }

    Task? mRecurringLoopTask;
    TimeSpan mOriginalRunInterval;
    CancellationToken mOriginalStartToken;
    CancellationTokenSource? mRecurringLoopCts;
    Exception? mLastException;
    volatile bool mIsRunning;
    volatile uint mTimesInvoked;

    readonly Action<CancellationToken> mAction;
    readonly string mTaskName;
    readonly object mSyncLock = new();
    readonly SemaphoreSlim mSemaphore = new(1, 1);

    readonly ILogger mLog;
}

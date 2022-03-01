
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace dotnetRpc.Core.Shared;

public class RecurringTask
{
    public bool IsRunning => mIsRunning;
    public uint TimesInvoked => mTimesInvoked;

    public RecurringTask(Func<CancellationToken, Task> action, string taskName)
    {
        mAction = action;
        mTaskName = taskName;

        mLog = RpcLoggerFactory.CreateLogger("RecurringTask");
    }

    public void Start(TimeSpan interval, CancellationToken ct)
    {
        Monitor.Enter(mSyncLock);
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
            Monitor.Exit(mSyncLock);
        }
    }

    public async Task Stop()
    {
        Monitor.Enter(mSyncLock);
        try
        {
            if (mRecurringLoopTask == null || mRecurringLoopTask.IsCompleted)
                return;

            if (mRecurringLoopCts == null)
                return;

            mRecurringLoopCts.Cancel();
            try
            {
                await mRecurringLoopTask;
            }
            finally
            {
                mRecurringLoopCts.Dispose();
                mRecurringLoopCts = null;
            }
        }
        finally
        {
            Monitor.Exit(mSyncLock);
        }
    }

    public async Task FireEarly() => await FireEarly(CancellationToken.None);

    public async Task FireEarly(CancellationToken ct)
    {
        Monitor.Enter(mSyncLock);
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
        }
        finally
        {
            Monitor.Exit(mSyncLock);
        }
    }

    async Task RunRecurringAsync(
        Func<CancellationToken, Task> action,
        TimeSpan interval,
        CancellationToken actionToken,
        CancellationToken breakToken)
    {
        mIsRunning = true;
        try
        {
            while (!breakToken.IsCancellationRequested)
            {
                await action(actionToken);
                mTimesInvoked++;

                await SafeDelay(interval, breakToken);
            }
        }
        finally
        {
            mIsRunning = false;
        }
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
    volatile bool mIsRunning;
    volatile uint mTimesInvoked;

    readonly Func<CancellationToken, Task> mAction;
    readonly string mTaskName;
    readonly ILogger mLog;
    readonly object mSyncLock = new();
}

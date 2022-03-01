using System;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Tests;

[TestFixture]
public class RecurringTaskTests
{
    [Test, Timeout(TestsConstants.Timeout)]
    public async Task Task_Starts_And_Stops()
    {
        TestRecurringTask testRecurringTask = new();
        RecurringTask recurringTask = new(
            testRecurringTask.InvokeAsync,
            nameof(TestRecurringTask));

        const int minimumInvokations = 5;
        const int timerIntervalMs = 20;

        recurringTask.Start(
            TimeSpan.FromMilliseconds(timerIntervalMs),
            CancellationToken.None);

        Assert.That(() => recurringTask.IsRunning, Is.True.After(100, 10));
        Assert.That(
            () => testRecurringTask.TimesInvoked,
            Is.GreaterThanOrEqualTo(minimumInvokations)
                .After(timerIntervalMs * (minimumInvokations + 1)));

        await recurringTask.Stop();
        
        Assert.That(() => recurringTask.IsRunning, Is.False.After(100, 10));

        Assert.That(
            recurringTask.TimesInvoked,
            Is.EqualTo(testRecurringTask.TimesInvoked));
    }

    [Test, Timeout(TestsConstants.Timeout)]
    public async Task Task_Starts_And_Can_Be_Cancelled()
    {
        CancellationTokenSource cts = new();

        TestRecurringTask testRecurringTask = new();
        RecurringTask recurringTask = new(
            testRecurringTask.InvokeAsync,
            nameof(TestRecurringTask));

        const int minimumInvokations = 5;
        const int timerIntervalMs = 20;
        
        recurringTask.Start(TimeSpan.FromMilliseconds(timerIntervalMs), cts.Token);

        Assert.That(() => recurringTask.IsRunning, Is.True.After(100, 10));
        Assert.That(
            () => testRecurringTask.TimesInvoked,
            Is.GreaterThanOrEqualTo(minimumInvokations)
                .After(timerIntervalMs * (minimumInvokations + 1)));

        cts.Cancel();

        Assert.That(() => recurringTask.IsRunning, Is.False.After(100, 10));

        int actualTimesInvoked = testRecurringTask.TimesInvoked;

        await Task.Delay(100);

        Assert.That(testRecurringTask.TimesInvoked, Is.EqualTo(actualTimesInvoked));
    }
}

class TestRecurringTask
{
    public int TimesInvoked = 0;

    public async Task InvokeAsync(CancellationToken ct)
    {
        await Task.Yield();
        Interlocked.Increment(ref TimesInvoked);
    }
}

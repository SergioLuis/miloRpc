using System;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using dotnetRpc.Core.Shared;

namespace dotnetRpc.Tests;

[TestFixture]
public class RecurringTaskTests
{
    [Test, Timeout(TestingConstants.Timeout)]
    public async Task Task_Starts_And_Stops()
    {
        RecurringTaskCountInvocations testRecurringTask = new();
        RecurringTask recurringTask = new(
            testRecurringTask.InvokeAsync,
            nameof(RecurringTaskCountInvocations));

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

        await recurringTask.StopAsync();
        
        Assert.That(() => recurringTask.IsRunning, Is.False.After(100, 10));

        Assert.That(
            recurringTask.TimesInvoked,
            Is.EqualTo(testRecurringTask.TimesInvoked));
    }

    [Test, Timeout(TestingConstants.Timeout)]
    public async Task Task_Starts_And_Can_Be_Cancelled()
    {
        CancellationTokenSource cts = new();

        RecurringTaskCountInvocations testRecurringTask = new();
        RecurringTask recurringTask = new(
            testRecurringTask.InvokeAsync,
            nameof(RecurringTaskCountInvocations));

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

    [Test, Timeout(TestingConstants.Timeout)]
    public async Task FireEarly_Works_And_Does_Not_Increment_TimesInvoked()
    {
        RecurringTaskCountInvocations testRecurringTask = new();
        RecurringTask recurringTask = new(
            testRecurringTask.InvokeAsync,
            nameof(RecurringTaskCountInvocations));

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

        await recurringTask.FireEarlyAsync();

        await recurringTask.StopAsync();
        
        Assert.That(() => recurringTask.IsRunning, Is.False.After(100, 10));

        Assert.That(
            recurringTask.TimesInvoked,
            Is.EqualTo(testRecurringTask.TimesInvoked - 1));
    }

    [Test, Timeout(TestingConstants.Timeout)]
    public async Task FireEarly_Throws_Exception_From_Recurring_Action()
    {
        CancellationTokenSource cts = new();

        RecurringTaskThrowsException testRecurringTask = new();
        RecurringTask recurringTask = new(
            testRecurringTask.InvokeAsync,
            nameof(RecurringTaskCountInvocations));

        const int minimumInvokations = 5;
        const int timerIntervalMs = 20;

        Assert.That(recurringTask.LastException, Is.Null);
        
        recurringTask.Start(TimeSpan.FromMilliseconds(timerIntervalMs), cts.Token);

        Assert.That(() => recurringTask.IsRunning, Is.True.After(100, 10));

        Assert.That(
            () => recurringTask.TimesInvoked,
            Is.GreaterThanOrEqualTo(minimumInvokations)
                .After(timerIntervalMs * (minimumInvokations + 1)));

        Assert.That(
            recurringTask.LastException,
            Is.Not.Null.And.TypeOf<InvalidOperationException>());

        Assert.That(
            () => recurringTask.FireEarlyAsync().GetAwaiter().GetResult(),
            Throws.InvalidOperationException);

        await recurringTask.StopAsync();

        Assert.That(() => recurringTask.IsRunning, Is.False.After(100, 10));

        uint actualTimesInvoked = recurringTask.TimesInvoked;

        await Task.Delay(100);

        Assert.That(recurringTask.TimesInvoked, Is.EqualTo(actualTimesInvoked));
    }

    [Test, Timeout(TestingConstants.Timeout)]
    public async Task Stopping_An_Errored_Loop_Does_Not_Throw_Exception()
    {
        CancellationTokenSource cts = new();

        RecurringTaskThrowsException testRecurringTask = new();
        RecurringTask recurringTask = new(
            testRecurringTask.InvokeAsync,
            nameof(RecurringTaskCountInvocations));

        const int minimumInvokations = 5;
        const int timerIntervalMs = 20;

        Assert.That(recurringTask.LastException, Is.Null);
        
        recurringTask.Start(TimeSpan.FromMilliseconds(timerIntervalMs), cts.Token);

        Assert.That(() => recurringTask.IsRunning, Is.True.After(100, 10));

        Assert.That(
            () => recurringTask.TimesInvoked,
            Is.GreaterThanOrEqualTo(minimumInvokations)
                .After(timerIntervalMs * (minimumInvokations + 1)));

        Assert.That(
            recurringTask.LastException,
            Is.Not.Null.And.TypeOf<InvalidOperationException>());

        await recurringTask.StopAsync();

        Assert.That(() => recurringTask.IsRunning, Is.False.After(100, 10));

        uint actualTimesInvoked = recurringTask.TimesInvoked;

        await Task.Delay(100);

        Assert.That(recurringTask.TimesInvoked, Is.EqualTo(actualTimesInvoked));
    }

    [Test, Timeout(TestingConstants.Timeout)]
    public async Task Task_Keeps_Running_Even_If_Errored()
    {
        CancellationTokenSource cts = new();

        RecurringTaskThrowsException testRecurringTask = new();
        RecurringTask recurringTask = new(
            testRecurringTask.InvokeAsync,
            nameof(RecurringTaskCountInvocations));

        const int minimumInvokations = 5;
        const int timerIntervalMs = 20;

        Assert.That(recurringTask.LastException, Is.Null);
        
        recurringTask.Start(TimeSpan.FromMilliseconds(timerIntervalMs), cts.Token);

        Assert.That(() => recurringTask.IsRunning, Is.True.After(100, 10));

        Assert.That(
            () => recurringTask.TimesInvoked,
            Is.GreaterThanOrEqualTo(minimumInvokations)
                .After(timerIntervalMs * (minimumInvokations + 1)));

        Assert.That(
            recurringTask.LastException,
            Is.Not.Null.And.TypeOf<InvalidOperationException>());

        cts.Cancel();

        Assert.That(() => recurringTask.IsRunning, Is.False.After(100, 10));

        uint actualTimesInvoked = recurringTask.TimesInvoked;

        await Task.Delay(100);

        Assert.That(recurringTask.TimesInvoked, Is.EqualTo(actualTimesInvoked));
    }
}

class RecurringTaskCountInvocations
{
    public int TimesInvoked = 0;

    public async Task InvokeAsync(CancellationToken ct)
    {
        await Task.Yield();
        Interlocked.Increment(ref TimesInvoked);
    }
}

class RecurringTaskThrowsException
{
    public async Task InvokeAsync(CancellationToken ct)
    {
        await Task.Yield();
        throw new InvalidOperationException(
            "The InvokeAsync operation is not supported");
    }
}

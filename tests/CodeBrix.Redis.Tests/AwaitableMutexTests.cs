using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class AwaitableMutexTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void isolated_sync_success_and_return()
    {
        using var mutex = AwaitableMutex.Create(timeoutMilliseconds: 100);

        for (var i = 0; i < 5; i++)
        {
            mutex.IsAvailable.Should().BeTrue();
            (i % 2 == 0 ? mutex.TryTakeInstant() : mutex.TryTakeSync()).Should().BeTrue();
            mutex.IsAvailable.Should().BeFalse();
            mutex.Release();
        }

        mutex.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task sync_caller_times_out_while_held()
    {
        using var mutex = AwaitableMutex.Create(timeoutMilliseconds: 50);
        mutex.TryTakeInstant().Should().BeTrue();

        var result = await WithTimeout(Task.Run(() => mutex.TryTakeSync(), TestContext.Current.CancellationToken));

        result.Should().BeFalse();
        mutex.IsAvailable.Should().BeFalse();
        mutex.Release();
        mutex.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task async_caller_times_out_while_held()
    {
        using var mutex = AwaitableMutex.Create(timeoutMilliseconds: 50);
        mutex.TryTakeInstant().Should().BeTrue();

        var result = await WithTimeout(mutex.TryTakeAsync().AsTask());

        result.Should().BeFalse();
        mutex.IsAvailable.Should().BeFalse();
        mutex.Release();
        mutex.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task disposal_prevents_new_acquisitions()
    {
        var mutex = AwaitableMutex.Create(timeoutMilliseconds: 100);
        mutex.TryTakeInstant().Should().BeTrue();

        mutex.Dispose();

        mutex.IsAvailable.Should().BeFalse();
        Assert.Throws<ObjectDisposedException>(() => mutex.TryTakeInstant());
        Assert.Throws<ObjectDisposedException>(() => mutex.TryTakeSync());
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await mutex.TryTakeAsync().AsTask());
        Assert.Throws<ObjectDisposedException>(() => mutex.Release());
    }

    [Fact]
    public async Task mixed_sync_and_async_waiters_are_released()
    {
        const int Iterations = 100;
        using var mutex = AwaitableMutex.Create(timeoutMilliseconds: 10_000);

        for (var i = 0; i < Iterations; i++)
        {
            await Core(i, mutex);
        }

        static async Task Core(int iteration, AwaitableMutex mutex)
        {
            mutex.TryTakeInstant().Should().BeTrue();

            var order = new List<string>();
            var expected = new[]
            {
                $"{iteration}:sync-1",
                $"{iteration}:async-1",
                $"{iteration}:sync-2",
                $"{iteration}:async-2",
            };

            var sync1 = StartSyncWaiter(mutex, expected[0], order, out var sync1Thread);
            WaitForBlocked(sync1Thread);

            var async1 = StartAsyncWaiter(mutex, expected[1], order);
            async1.IsCompleted.Should().BeFalse();

            var sync2 = StartSyncWaiter(mutex, expected[2], order, out var sync2Thread);
            WaitForBlocked(sync2Thread);

            var async2 = StartAsyncWaiter(mutex, expected[3], order);
            async2.IsCompleted.Should().BeFalse();

            mutex.Release();

            await WithTimeout(Task.WhenAll(sync1, async1, sync2, async2));

            // SemaphoreSlim does not guarantee FIFO ordering; this only verifies that every queued waiter arrives.
            order.Sort(StringComparer.Ordinal);
            Array.Sort(expected, StringComparer.Ordinal);
            order.Should().Equal(expected);
            mutex.IsAvailable.Should().BeTrue();
        }
    }

    private static Task StartSyncWaiter(AwaitableMutex mutex, string name, List<string> order, out Thread thread)
    {
        var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new ManualResetEventSlim();
        thread = new Thread(() =>
        {
            started.Set();
            try
            {
                if (!mutex.TryTakeSync()) throw new TimeoutException();

                Add(order, name);
                mutex.Release();
                source.TrySetResult(true);
            }
            catch (Exception ex)
            {
                source.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = name,
        };
        thread.Start();

        started.Wait(TestTimeout, TestContext.Current.CancellationToken).Should().BeTrue($"{name} did not start");
        return source.Task;
    }

    private static async Task StartAsyncWaiter(AwaitableMutex mutex, string name, List<string> order)
    {
        if (!await mutex.TryTakeAsync().AsTask()) throw new TimeoutException();

        Add(order, name);
        mutex.Release();
    }

    private static void WaitForBlocked(Thread thread)
    {
        SpinWait.SpinUntil(() => (thread.ThreadState & ThreadState.WaitSleepJoin) != 0, TestTimeout).Should().BeTrue($"{thread.Name} did not block");
    }

    private static void Add(List<string> order, string name)
    {
        lock (order)
        {
            order.Add(name);
        }
    }

    private static async Task WithTimeout(Task task)
    {
        var timeout = Task.Delay(TestTimeout, TestContext.Current.CancellationToken);
        var first = await Task.WhenAny(task, timeout);
        first.Should().BeSameAs(task);
        await task;
    }

    private static async Task<T> WithTimeout<T>(Task<T> task)
    {
        var timeout = Task.Delay(TestTimeout, TestContext.Current.CancellationToken);
        var first = await Task.WhenAny(task, timeout);
        first.Should().BeSameAs(task);
        return await task;
    }
}

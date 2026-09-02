using System;
using System.Threading;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ResultBoxTests
{
    [Fact]
    public void sync_result_box()
    {
        var msg = Message.Create(-1, CommandFlags.None, RedisCommand.PING);
        var box = SimpleResultBox<string>.Get();
        box.IsAsync.Should().BeFalse();

        int activated = 0;
        lock (box)
        {
            Task.Run(() =>
            {
                lock (box)
                {
                    // release the worker to start work
                    Monitor.PulseAll(box);

                    // wait for the completion signal
                    if (Monitor.Wait(box, TimeSpan.FromSeconds(10)))
                    {
                        Interlocked.Increment(ref activated);
                    }
                }
            }, TestContext.Current.CancellationToken);
            Monitor.Wait(box, TimeSpan.FromSeconds(10)).Should().BeTrue("failed to handover lock to worker");
        }

        // check that continuation was not already signalled
        Thread.Sleep(100);
        Volatile.Read(ref activated).Should().Be(0);

        msg.SetSource(ResultProcessor.DemandOK, box);
        msg.TrySetResult("abc").Should().BeTrue();

        // check that TrySetResult did not signal continuation
        Thread.Sleep(100);
        Volatile.Read(ref activated).Should().Be(0);

        // check that complete signals continuation
        msg.Complete(null);
        Thread.Sleep(100);
        Volatile.Read(ref activated).Should().Be(1);

        var s = box.GetResult(out var ex);
        ex.Should().BeNull();
        s.Should().NotBeNull();
        s.Should().Be("abc");
    }

    [Fact]
    public void task_result_box()
    {
        // TaskResultBox currently uses a stating field for values before activations are
        // signalled; High Integrity Mode *demands* this behaviour, so: validate that it
        // works correctly
        var msg = Message.Create(-1, CommandFlags.None, RedisCommand.PING);
        var box = TaskResultBox<string>.Create(out var tcs, null);
        box.IsAsync.Should().BeTrue();

        msg.SetSource(ResultProcessor.DemandOK, box);
        msg.TrySetResult("abc").Should().BeTrue();

        // check that continuation was not already signalled
        Thread.Sleep(100);
        tcs.Task.IsCompleted.Should().BeFalse();

        msg.SetSource(ResultProcessor.DemandOK, box);
        msg.TrySetResult("abc").Should().BeTrue();

        // check that TrySetResult did not signal continuation
        Thread.Sleep(100);
        tcs.Task.IsCompleted.Should().BeFalse();

        // check that complete signals continuation
        msg.Complete(null);
        Thread.Sleep(100);
        tcs.Task.IsCompleted.Should().BeTrue();

        var s = box.GetResult(out var ex);
        ex.Should().BeNull();
        s.Should().NotBeNull();
        s.Should().Be("abc");

        tcs.Task.Result.Should().Be("abc"); // we already checked IsCompleted
    }
}

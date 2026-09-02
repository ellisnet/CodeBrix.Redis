using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CodeBrix.Redis.Maintenance;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class PubSubTests(ITestOutputHelper output, SharedConnectionFixture fixture)
    : PubSubTestBase(output, fixture, null)
{
}

[RunPerProtocol]
public class InProcPubSubTests(ITestOutputHelper output, InProcServerFixture fixture)
    : PubSubTestBase(output, null, fixture)
{
    protected override bool UseDedicatedInProcessServer => true;
}

[RunPerProtocol]
public abstract class PubSubTestBase(
    ITestOutputHelper output,
    SharedConnectionFixture? connection,
    InProcServerFixture? server)
    : TestBase(output, connection, server)
{
    [Fact]
    public async Task explicit_publish_mode()
    {
        await using var conn = ConnectFactory(channelPrefix: "foo:");

        var pub = conn.GetSubscriber();
        int a = 0, b = 0, c = 0, d = 0;
        pub.Subscribe(new RedisChannel("*bcd", RedisChannel.PatternMode.Literal), (x, y) => Interlocked.Increment(ref a));
        pub.Subscribe(new RedisChannel("a*cd", RedisChannel.PatternMode.Pattern), (x, y) => Interlocked.Increment(ref b));
        pub.Subscribe(new RedisChannel("ab*d", RedisChannel.PatternMode.Auto), (x, y) => Interlocked.Increment(ref c));
        pub.Subscribe(RedisChannel.Pattern("abc*"), (x, y) => Interlocked.Increment(ref d));

        pub.Publish(RedisChannel.Literal("abcd"), "efg");
        await UntilConditionAsync(
            TimeSpan.FromSeconds(10),
            () => Volatile.Read(ref b) == 1
               && Volatile.Read(ref c) == 1
               && Volatile.Read(ref d) == 1);
        Volatile.Read(ref a).Should().Be(0);
        Volatile.Read(ref b).Should().Be(1);
        Volatile.Read(ref c).Should().Be(1);
        Volatile.Read(ref d).Should().Be(1);

        pub.Publish(RedisChannel.Pattern("*bcd"), "efg");
        await UntilConditionAsync(TimeSpan.FromSeconds(10), () => Volatile.Read(ref a) == 1);
        Volatile.Read(ref a).Should().Be(1);
    }

    [Theory]
    [InlineData(null, false, "a")]
    [InlineData("", false, "b")]
    [InlineData("Foo:", false, "c")]
    [InlineData(null, true, "d")]
    [InlineData("", true, "e")]
    [InlineData("Foo:", true, "f")]
    public async Task test_basic_pub_sub(string? channelPrefix, bool wildCard, string breaker)
    {
        await using var conn = ConnectFactory(channelPrefix: channelPrefix, shared: false);

        var pub = GetAnyPrimary(conn.DefaultClient);
        var sub = conn.GetSubscriber();
        await PingAsync(pub, sub).ForAwait();
        HashSet<string?> received = [];
        int secondHandler = 0;
        //explicit channel construction: the implicit string -> RedisChannel conversion is [Obsolete]
        //("specify a PatternMode, or use Literal/Pattern"). PatternMode.Auto is exactly what that
        //conversion applied, and it is what this test needs - subChannel is a pattern only when
        //wildCard is true.
        RedisChannel subChannel = new((wildCard ? "a*c" : "abc") + breaker, RedisChannel.PatternMode.Auto);
        RedisChannel pubChannel = RedisChannel.Literal("abc" + breaker);
        Action<RedisChannel, RedisValue> handler1 = (channel, payload) =>
        {
            lock (received)
            {
                if (channel == pubChannel)
                {
                    received.Add(payload);
                }
                else
                {
                    Log(channel);
                }
            }
        }, handler2 = (_, __) => Interlocked.Increment(ref secondHandler);
        sub.Subscribe(subChannel, handler1);
        sub.Subscribe(subChannel, handler2);

        lock (received)
        {
            received.Should().BeEmpty();
        }
        Volatile.Read(ref secondHandler).Should().Be(0);
        var count = sub.Publish(pubChannel, "def");

        await PingAsync(pub, sub, 3).ForAwait();

        await UntilConditionAsync(TimeSpan.FromSeconds(5), () => received.Count == 1);
        lock (received)
        {
            received.Should().ContainSingle();
        }
        // Give handler firing a moment
        await UntilConditionAsync(TimeSpan.FromSeconds(2), () => Volatile.Read(ref secondHandler) == 1);
        Volatile.Read(ref secondHandler).Should().Be(1);

        // unsubscribe from first; should still see second
        sub.Unsubscribe(subChannel, handler1);
        count = sub.Publish(pubChannel, "ghi");
        await PingAsync(pub, sub).ForAwait();
        lock (received)
        {
            received.Should().ContainSingle();
        }

        await UntilConditionAsync(TimeSpan.FromSeconds(2), () => Volatile.Read(ref secondHandler) == 2);

        var secondHandlerCount = Volatile.Read(ref secondHandler);
        Log("Expecting 2 from second handler, got: " + secondHandlerCount);
        secondHandlerCount.Should().Be(2);
        count.Should().Be(1);

        // unsubscribe from second; should see nothing this time
        sub.Unsubscribe(subChannel, handler2);
        count = sub.Publish(pubChannel, "ghi");
        await PingAsync(pub, sub).ForAwait();
        lock (received)
        {
            received.Should().ContainSingle();
        }
        secondHandlerCount = Volatile.Read(ref secondHandler);
        Log("Expecting 2 from second handler, got: " + secondHandlerCount);
        secondHandlerCount.Should().Be(2);
        count.Should().Be(0);
    }

    [Fact]
    public async Task ping()
    {
        await using var conn = ConnectFactory(shared: false);
        var pub = GetAnyPrimary(conn.DefaultClient);
        var sub = conn.GetSubscriber();

        await PingAsync(pub, sub, 5).ForAwait();
        await sub.SubscribeAsync(RedisChannel.Literal(Me()), (_, __) => { }); // to ensure we're in subscriber mode
        await PingAsync(pub, sub, 5).ForAwait();
    }

    [Fact]
    public async Task test_basic_pub_sub_fire_and_forget()
    {
        await using var conn = ConnectFactory(shared: false);

        var profiler = conn.DefaultClient.AddProfiler();
        var pub = GetAnyPrimary(conn.DefaultClient);
        var sub = conn.GetSubscriber();

        RedisChannel key = RedisChannel.Literal(Me() + Guid.NewGuid());
        HashSet<string?> received = [];
        int secondHandler = 0;
        await PingAsync(pub, sub).ForAwait();
        sub.Subscribe(
            key,
            (channel, payload) =>
            {
                lock (received)
                {
                    if (channel == key)
                    {
                        received.Add(payload);
                    }
                }
            },
            CommandFlags.FireAndForget);

        sub.Subscribe(key, (_, __) => Interlocked.Increment(ref secondHandler), CommandFlags.FireAndForget);
        Log(profiler);

        lock (received)
        {
            received.Should().BeEmpty();
        }
        Volatile.Read(ref secondHandler).Should().Be(0);
        await PingAsync(pub, sub).ForAwait();
        var count = sub.Publish(key, "def", CommandFlags.FireAndForget);
        await PingAsync(pub, sub).ForAwait();

        await UntilConditionAsync(TimeSpan.FromSeconds(5), () => received.Count == 1);
        Log(profiler);

        lock (received)
        {
            received.Should().ContainSingle();
        }
        Volatile.Read(ref secondHandler).Should().Be(1);

        sub.Unsubscribe(key);
        count = sub.Publish(key, "ghi", CommandFlags.FireAndForget);

        await PingAsync(pub, sub).ForAwait();
        Log(profiler);
        lock (received)
        {
            received.Should().ContainSingle();
        }
        count.Should().Be(0);
    }

    private async Task PingAsync(IServer pub, ISubscriber sub, int times = 1)
    {
        while (times-- > 0)
        {
            // both use async because we want to drain the completion managers, and the only
            // way to prove that is to use TPL objects
            var subTask = sub.PingAsync();
            var pubTask = pub.PingAsync();
            try
            {
                await Task.WhenAll(subTask, pubTask).ForAwait();
            }
            catch (TimeoutException ex)
            {
                throw new TimeoutException($"Timeout; sub: {GetState(subTask)}, pub: {GetState(pubTask)}", ex);
            }

            Log($"sub: {GetState(subTask)}, pub: {GetState(pubTask)}");

            static string GetState(Task<TimeSpan> pending)
            {
                var status = pending.Status;
                return status switch
                {
                    TaskStatus.RanToCompletion => $"{status} in {pending.Result.TotalMilliseconds:###,##0.0}ms)",
                    TaskStatus.Faulted when pending.Exception is { InnerExceptions.Count:1 } ae => $"{status}: {ae.InnerExceptions[0].Message}",
                    TaskStatus.Faulted => $"{status}: {pending.Exception?.Message}",
                    _ => status.ToString(),
                };
            }
        }
    }

    [Fact]
    public async Task test_pattern_pub_sub()
    {
        await using var conn = ConnectFactory(shared: false);

        var pub = GetAnyPrimary(conn.DefaultClient);
        var sub = conn.GetSubscriber();

        HashSet<string?> received = [];
        int secondHandler = 0;
        sub.Subscribe(RedisChannel.Pattern("a*c"), (channel, payload) =>
        {
            lock (received)
            {
                if (channel == "abc")
                {
                    received.Add(payload);
                }
            }
        });

        sub.Subscribe(RedisChannel.Pattern("a*c"), (_, __) => Interlocked.Increment(ref secondHandler));
        lock (received)
        {
            received.Should().BeEmpty();
        }
        Volatile.Read(ref secondHandler).Should().Be(0);

        await PingAsync(pub, sub).ForAwait();
        var count = sub.Publish(RedisChannel.Literal("abc"), "def");
        await PingAsync(pub, sub).ForAwait();

        await UntilConditionAsync(TimeSpan.FromSeconds(5), () => received.Count == 1);
        lock (received)
        {
            received.Should().ContainSingle();
        }

        // Give reception a bit, the handler could be delayed under load
        await UntilConditionAsync(TimeSpan.FromSeconds(2), () => Volatile.Read(ref secondHandler) == 1);
        Volatile.Read(ref secondHandler).Should().Be(1);

        sub.Unsubscribe(RedisChannel.Pattern("a*c"));
        count = sub.Publish(RedisChannel.Literal("abc"), "ghi");

        await PingAsync(pub, sub).ForAwait();

        lock (received)
        {
            received.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task test_publish_with_no_subscribers()
    {
        await using var conn = ConnectFactory();

        var sub = conn.GetSubscriber();
        sub.Publish(RedisChannel.Literal(Me() + "channel"), "message").Should().Be(0);
    }

    [Fact]
    public async Task test_massive_publish_with_without_flush_local()
    {
        Skip.UnlessLongRunning();
        await using var conn = ConnectFactory();

        var sub = conn.GetSubscriber();
        TestMassivePublish(sub, RedisChannel.Literal(Me()), "local");
    }

    [Fact]
    public async Task test_massive_publish_with_without_flush_remote()
    {
        Skip.UnlessLongRunning();
        SkipIfWouldUseInProcessServer();
        await using var conn = Create(configuration: TestConfig.Current.RemoteServerAndPort);

        var sub = conn.GetSubscriber();
        TestMassivePublish(sub, RedisChannel.Literal(Me()), "remote");
    }

    private void TestMassivePublish(ISubscriber sub, RedisChannel channel, string caption)
    {
        const int loop = 10000;

        var tasks = new Task[loop];

        var withFAF = Stopwatch.StartNew();
        for (int i = 0; i < loop; i++)
        {
            sub.Publish(channel, "bar", CommandFlags.FireAndForget);
        }
        withFAF.Stop();

        var withAsync = Stopwatch.StartNew();
        for (int i = 0; i < loop; i++)
        {
            tasks[i] = sub.PublishAsync(channel, "bar");
        }
        sub.WaitAll(tasks);
        withAsync.Stop();

        Log($"{caption}: {withFAF.ElapsedMilliseconds}ms (F+F) vs {withAsync.ElapsedMilliseconds}ms (async)");
        // We've made async so far, this test isn't really valid anymore
        // So let's check they're at least within a few seconds.
        (withFAF.ElapsedMilliseconds < withAsync.ElapsedMilliseconds + 3000).Should().BeTrue(caption);
    }

    [Fact]
    public async Task subscribe_async_enumerable()
    {
        await using var conn = ConnectFactory(shared: false);

        var sub = conn.GetSubscriber();
        RedisChannel channel = RedisChannel.Literal(Me());

        const int TO_SEND = 5;
        var gotall = new TaskCompletionSource<int>();

        var source = await sub.SubscribeAsync(channel);
        var op = Task.Run(async () =>
        {
            int count = 0;
            await foreach (var item in source.WithCancellation(TestContext.Current.CancellationToken))
            {
                count++;
                if (count == TO_SEND) gotall.TrySetResult(count);
            }
            return count;
        }, TestContext.Current.CancellationToken);

        for (int i = 0; i < TO_SEND; i++)
        {
            await sub.PublishAsync(channel, i);
        }
        await gotall.Task.WithTimeout(5000);

        // check the enumerator exits cleanly
        sub.Unsubscribe(channel);
        var count = await op.WithTimeout(1000);
        count.Should().Be(5);
    }

    [Fact]
    public async Task pub_sub_get_all_any_order()
    {
        await using var conn = ConnectFactory(shared: false);

        var sub = conn.GetSubscriber();
        RedisChannel channel = RedisChannel.Literal(Me());
        const int count = 1000;
        var syncLock = new object();

        // The subscription connection is a separate connection from the interactive one, and can still
        // be coming up when the connect call returns; asserting it immediately is a race that a slow or
        // contended machine loses (this is the "IsConnected" failure seen on the Windows CI job).
        await UntilConditionAsync(TimeSpan.FromSeconds(10), () => sub.IsConnected()).ForAwait();
        sub.IsConnected().Should().BeTrue(nameof(sub.IsConnected));
        var data = new HashSet<int>();
        await sub.SubscribeAsync(channel, (_, val) =>
        {
            bool pulse;
            lock (data)
            {
                data.Add(int.Parse(Encoding.UTF8.GetString(val!)));
                pulse = data.Count == count;
                if ((data.Count % 100) == 99) Log(data.Count.ToString());
            }
            if (pulse)
            {
                lock (syncLock)
                {
                    Monitor.PulseAll(syncLock);
                }
            }
        }).ForAwait();

        lock (syncLock)
        {
            for (int i = 0; i < count; i++)
            {
                sub.Publish(channel, i.ToString(), CommandFlags.FireAndForget);
            }
            sub.Ping();
            if (!Monitor.Wait(syncLock, 20000))
            {
                throw new TimeoutException("Items: " + data.Count);
            }
            for (int i = 0; i < count; i++)
            {
                data.Should().Contain(i);
            }
        }
    }

    [Fact]
    public async Task pub_sub_get_all_correct_order()
    {
        SkipIfWouldUseInProcessServer();
        await using (var conn = Create(configuration: TestConfig.Current.RemoteServerAndPort, syncTimeout: 20000, log: Writer))
        {
            var sub = conn.GetSubscriber();
            RedisChannel channel = RedisChannel.Literal(Me());
            const int count = 250;
            var syncLock = new object();

            var data = new List<int>(count);
            var subChannel = await sub.SubscribeAsync(channel).ForAwait();

            await sub.PingAsync().ForAwait();

            async Task RunLoop()
            {
                while (!subChannel.Completion.IsCompleted)
                {
                    var work = await subChannel.ReadAsync(TestContext.Current.CancellationToken).ForAwait();
                    int i = int.Parse(Encoding.UTF8.GetString(work.Message!));
                    lock (data)
                    {
                        data.Add(i);
                        if (data.Count == count) break;
                        if ((data.Count % 100) == 99) Log("Received: " + data.Count.ToString());
                    }
                }
                lock (syncLock)
                {
                    Log("PulseAll.");
                    Monitor.PulseAll(syncLock);
                }
            }

            lock (syncLock)
            {
                // Intentionally not awaited - running in parallel
                _ = Task.Run(RunLoop, TestContext.Current.CancellationToken);
                for (int i = 0; i < count; i++)
                {
                    sub.Publish(channel, i.ToString());
                    if ((i % 100) == 99) Log("Published: " + i.ToString());
                }
                Log("Send loop complete.");
                if (!Monitor.Wait(syncLock, 20000))
                {
                    throw new TimeoutException("Items: " + data.Count);
                }
                Log("Unsubscribe.");
                subChannel.Unsubscribe();
                Log("Sub Ping.");
                sub.Ping();
                Log("Database Ping.");
                conn.GetDatabase().Ping();
                for (int i = 0; i < count; i++)
                {
                    data[i].Should().Be(i);
                }
            }

            Log("Awaiting completion.");
            await subChannel.Completion;
            Log("Completion awaited.");
            await Assert.ThrowsAsync<ChannelClosedException>(async () => await subChannel.ReadAsync(TestContext.Current.CancellationToken).ForAwait()).ForAwait();
            Log("End of muxer.");
        }
        Log("End of test.");
    }

    [Fact]
    public async Task pub_sub_get_all_correct_order_on_message_sync()
    {
        SkipIfWouldUseInProcessServer();
        await using (var conn = Create(configuration: TestConfig.Current.RemoteServerAndPort, syncTimeout: 20000, log: Writer))
        {
            var sub = conn.GetSubscriber();
            RedisChannel channel = RedisChannel.Literal(Me());
            const int count = 1000;
            var syncLock = new object();

            var data = new List<int>(count);
            var subChannel = await sub.SubscribeAsync(channel).ForAwait();
            subChannel.OnMessage(msg =>
            {
                int i = int.Parse(Encoding.UTF8.GetString(msg.Message!));
                bool pulse = false;
                lock (data)
                {
                    data.Add(i);
                    if (data.Count == count) pulse = true;
                    if ((data.Count % 100) == 99) Log("Received: " + data.Count.ToString());
                }
                if (pulse)
                {
                    lock (syncLock)
                    {
                        Monitor.PulseAll(syncLock);
                    }
                }
            });
            await sub.PingAsync().ForAwait();

            lock (syncLock)
            {
                for (int i = 0; i < count; i++)
                {
                    sub.Publish(channel, i.ToString(), CommandFlags.FireAndForget);
                    if ((i % 100) == 99) Log("Published: " + i.ToString());
                }
                Log("Send loop complete.");
                if (!Monitor.Wait(syncLock, 20000))
                {
                    throw new TimeoutException("Items: " + data.Count);
                }
                Log("Unsubscribe.");
                subChannel.Unsubscribe();
                Log("Sub Ping.");
                sub.Ping();
                Log("Database Ping.");
                conn.GetDatabase().Ping();
                for (int i = 0; i < count; i++)
                {
                    data[i].Should().Be(i);
                }
            }

            Log("Awaiting completion.");
            await subChannel.Completion;
            Log("Completion awaited.");
            subChannel.Completion.IsCompleted.Should().BeTrue();
            await Assert.ThrowsAsync<ChannelClosedException>(async () => await subChannel.ReadAsync(TestContext.Current.CancellationToken).ForAwait()).ForAwait();
            Log("End of muxer.");
        }
        Log("End of test.");
    }

    [Fact]
    public async Task pub_sub_get_all_correct_order_on_message_async()
    {
        SkipIfWouldUseInProcessServer();
        await using (var conn = Create(configuration: TestConfig.Current.RemoteServerAndPort, syncTimeout: 20000, log: Writer))
        {
            var sub = conn.GetSubscriber();
            RedisChannel channel = RedisChannel.Literal(Me());
            const int count = 1000;
            var syncLock = new object();

            var data = new List<int>(count);
            var subChannel = await sub.SubscribeAsync(channel).ForAwait();
            subChannel.OnMessage(msg =>
            {
                int i = int.Parse(Encoding.UTF8.GetString(msg.Message!));
                bool pulse = false;
                lock (data)
                {
                    data.Add(i);
                    if (data.Count == count) pulse = true;
                    if ((data.Count % 100) == 99) Log("Received: " + data.Count.ToString());
                }
                if (pulse)
                {
                    lock (syncLock)
                    {
                        Monitor.PulseAll(syncLock);
                    }
                }
                // Making sure we cope with null being returned here by a handler
                return i % 2 == 0 ? null! : Task.CompletedTask;
            });
            await sub.PingAsync().ForAwait();

            // Give a delay between subscriptions and when we try to publish to be safe
            await Task.Delay(1000, TestContext.Current.CancellationToken).ForAwait();

            lock (syncLock)
            {
                for (int i = 0; i < count; i++)
                {
                    sub.Publish(channel, i.ToString(), CommandFlags.FireAndForget);
                    if ((i % 100) == 99) Log("Published: " + i.ToString());
                }
                Log("Send loop complete.");
                if (!Monitor.Wait(syncLock, 20000))
                {
                    throw new TimeoutException("Items: " + data.Count);
                }
                Log("Unsubscribe.");
                subChannel.Unsubscribe();
                Log("Sub Ping.");
                sub.Ping();
                Log("Database Ping.");
                conn.GetDatabase().Ping();
                for (int i = 0; i < count; i++)
                {
                    data[i].Should().Be(i);
                }
            }

            Log("Awaiting completion.");
            await subChannel.Completion;
            Log("Completion awaited.");
            subChannel.Completion.IsCompleted.Should().BeTrue();
            await Assert.ThrowsAsync<ChannelClosedException>(async () => await subChannel.ReadAsync(TestContext.Current.CancellationToken).ForAwait()).ForAwait();
            Log("End of muxer.");
        }
        Log("End of test.");
    }

    [Fact]
    public async Task test_publish_with_subscribers()
    {
        await using var pair = ConnectFactory(shared: false);
        await using var connA = pair.DefaultClient;
        await using var connB = pair.CreateClient();
        await using var connPub = pair.CreateClient();

        var channel = RedisChannel.Literal(Me());
        var listenA = connA.GetSubscriber();
        var listenB = connB.GetSubscriber();
        var t1 = listenA.SubscribeAsync(channel, (arg1, arg2) => { });
        var t2 = listenB.SubscribeAsync(channel, (arg1, arg2) => { });

        await Task.WhenAll(t1, t2).ForAwait();

        // subscribe is just a thread-race-mess
        await listenA.PingAsync();
        await listenB.PingAsync();

        var pub = connPub.GetSubscriber().PublishAsync(channel, "message");
        (await pub).Should().Be(2); // delivery count
    }

    [Fact]
    public async Task test_multiple_subscribers_get_message()
    {
        await using var pair = ConnectFactory(shared: false);
        await using var connA = pair.DefaultClient;
        await using var connB = pair.CreateClient();
        await using var connPub = pair.CreateClient();

        var channel = RedisChannel.Literal(Me());
        var listenA = connA.GetSubscriber();
        var listenB = connB.GetSubscriber();
        await connPub.GetDatabase().PingAsync();
        var pub = connPub.GetSubscriber();
        int gotA = 0, gotB = 0;
        var tA = listenA.SubscribeAsync(channel, (_, msg) => { if (msg == "message") Interlocked.Increment(ref gotA); });
        var tB = listenB.SubscribeAsync(channel, (_, msg) => { if (msg == "message") Interlocked.Increment(ref gotB); });
        await Task.WhenAll(tA, tB).ForAwait();
        pub.Publish(channel, "message").Should().Be(2);
        await AllowReasonableTimeToPublishAndProcess().ForAwait();
        Volatile.Read(ref gotA).Should().Be(1);
        Volatile.Read(ref gotB).Should().Be(1);

        // and unsubscribe...
        tA = listenA.UnsubscribeAsync(channel);
        await tA;
        pub.Publish(channel, "message").Should().Be(1);
        await AllowReasonableTimeToPublishAndProcess().ForAwait();
        Volatile.Read(ref gotA).Should().Be(1);
        Volatile.Read(ref gotB).Should().Be(2);
    }

    [Fact]
    public async Task issue38()
    {
        await using var conn = ConnectFactory();

        var sub = conn.GetSubscriber();
        int count = 0;
        var prefix = Me();
        void Handler(RedisChannel unused, RedisValue unused2) => Interlocked.Increment(ref count);
        var a0 = sub.SubscribeAsync(RedisChannel.Literal(prefix + "foo"), Handler);
        var a1 = sub.SubscribeAsync(RedisChannel.Literal(prefix + "bar"), Handler);
        var b0 = sub.SubscribeAsync(RedisChannel.Pattern(prefix + "f*o"), Handler);
        var b1 = sub.SubscribeAsync(RedisChannel.Pattern(prefix + "b*r"), Handler);
        await Task.WhenAll(a0, a1, b0, b1).ForAwait();

        var c = sub.PublishAsync(RedisChannel.Literal(prefix + "foo"), "foo");
        var d = sub.PublishAsync(RedisChannel.Literal(prefix + "f@o"), "f@o");
        var e = sub.PublishAsync(RedisChannel.Literal(prefix + "bar"), "bar");
        var f = sub.PublishAsync(RedisChannel.Literal(prefix + "b@r"), "b@r");
        await Task.WhenAll(c, d, e, f).ForAwait();

        long total = c.Result + d.Result + e.Result + f.Result;

        await AllowReasonableTimeToPublishAndProcess().ForAwait();

        total.Should().Be(6); // sent
        Volatile.Read(ref count).Should().Be(6); // received
    }

    internal static Task AllowReasonableTimeToPublishAndProcess() => Task.Delay(500, TestContext.Current.CancellationToken);

    [Fact]
    public async Task test_partial_subscriber_get_message()
    {
        await using var pair = ConnectFactory();
        await using var connA = pair.DefaultClient;
        await using var connB = pair.CreateClient();
        await using var connPub = pair.CreateClient();

        int gotA = 0, gotB = 0;
        var listenA = connA.GetSubscriber();
        var listenB = connB.GetSubscriber();
        var pub = connPub.GetSubscriber();
        var prefix = Me();
        var tA = listenA.SubscribeAsync(RedisChannel.Literal(prefix + "channel"), (s, msg) => { if (s == prefix + "channel" && msg == "message") Interlocked.Increment(ref gotA); });
        var tB = listenB.SubscribeAsync(RedisChannel.Pattern(prefix + "chann*"), (s, msg) => { if (s == prefix + "channel" && msg == "message") Interlocked.Increment(ref gotB); });
        await Task.WhenAll(tA, tB).ForAwait();
        pub.Publish(RedisChannel.Literal(prefix + "channel"), "message").Should().Be(2);
        await AllowReasonableTimeToPublishAndProcess().ForAwait();
        Volatile.Read(ref gotA).Should().Be(1);
        Volatile.Read(ref gotB).Should().Be(1);

        // and unsubscibe...
        tB = listenB.UnsubscribeAsync(RedisChannel.Pattern(prefix + "chann*"), null);
        await tB;
        pub.Publish(RedisChannel.Literal(prefix + "channel"), "message").Should().Be(1);
        await AllowReasonableTimeToPublishAndProcess().ForAwait();
        Volatile.Read(ref gotA).Should().Be(2);
        Volatile.Read(ref gotB).Should().Be(1);
    }

    [Fact]
    public async Task test_subscribe_unsubscribe_and_subscribe_again()
    {
        await using var pair = ConnectFactory();
        await using var connPub = pair.DefaultClient;
        await using var connSub = pair.CreateClient();

        var prefix = Me();
        var pub = connPub.GetSubscriber();
        var sub = connSub.GetSubscriber();
        int x = 0, y = 0;
        var t1 = sub.SubscribeAsync(RedisChannel.Literal(prefix + "abc"), (arg1, arg2) => Interlocked.Increment(ref x));
        var t2 = sub.SubscribeAsync(RedisChannel.Pattern(prefix + "ab*"), (arg1, arg2) => Interlocked.Increment(ref y));
        await Task.WhenAll(t1, t2).ForAwait();
        pub.Publish(RedisChannel.Literal(prefix + "abc"), "");
        await AllowReasonableTimeToPublishAndProcess().ForAwait();
        Volatile.Read(ref x).Should().Be(1);
        Volatile.Read(ref y).Should().Be(1);
        t1 = sub.UnsubscribeAsync(RedisChannel.Literal(prefix + "abc"), null);
        t2 = sub.UnsubscribeAsync(RedisChannel.Pattern(prefix + "ab*"), null);
        await Task.WhenAll(t1, t2).ForAwait();
        pub.Publish(RedisChannel.Literal(prefix + "abc"), "");
        Volatile.Read(ref x).Should().Be(1);
        Volatile.Read(ref y).Should().Be(1);
        t1 = sub.SubscribeAsync(RedisChannel.Literal(prefix + "abc"), (arg1, arg2) => Interlocked.Increment(ref x));
        t2 = sub.SubscribeAsync(RedisChannel.Pattern(prefix + "ab*"), (arg1, arg2) => Interlocked.Increment(ref y));
        await Task.WhenAll(t1, t2).ForAwait();
        pub.Publish(RedisChannel.Literal(prefix + "abc"), "");
        await AllowReasonableTimeToPublishAndProcess().ForAwait();
        Volatile.Read(ref x).Should().Be(2);
        Volatile.Read(ref y).Should().Be(2);
    }

    [Fact]
    public async Task azure_redis_events_automatic_subscribe()
    {
        Skip.IfNoConfig(nameof(TestConfig.Config.AzureCacheServer), TestConfig.Current.AzureCacheServer);
        Skip.IfNoConfig(nameof(TestConfig.Config.AzureCachePassword), TestConfig.Current.AzureCachePassword);

        bool didUpdate = false;
        var options = new ConfigurationOptions()
        {
            EndPoints = { TestConfig.Current.AzureCacheServer },
            Password = TestConfig.Current.AzureCachePassword,
            Ssl = true,
        };

        using (var connection = await ConnectionMultiplexer.ConnectAsync(options))
        {
            connection.ServerMaintenanceEvent += (_, e) =>
            {
                if (e is AzureMaintenanceEvent)
                {
                    didUpdate = true;
                }
            };

            var pubSub = connection.GetSubscriber();
            await pubSub.PublishAsync(RedisChannel.Literal("AzureRedisEvents"), "HI");
            await Task.Delay(100, TestContext.Current.CancellationToken);

            didUpdate.Should().BeTrue();
        }
    }
}

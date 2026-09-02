using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
[Collection(NonParallelCollection.Name)]
public class HotKeysClusterTests(ITestOutputHelper output, SharedConnectionFixture fixture) : HotKeysTests(output, fixture)
{
    protected override string GetConfiguration() => GetClusterConfiguration();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void can_use_cluster_filter(bool sample)
    {
        NoConcurrentRuntime();

        var key = Me();
        using var muxer = GetServer(key, out var server);
        Log($"server: {Format.ToString(server.EndPoint)}, key: '{key}'");

        var slot = muxer.HashSlot(key);
        server.HotKeysStart(slots: [(short)slot], sampleRatio: sample ? 3 : 1, duration: Duration);

        var db = muxer.GetDatabase();
        db.KeyDelete(key, flags: CommandFlags.FireAndForget);
        for (int i = 0; i < 20; i++)
        {
            db.StringIncrement(key, flags: CommandFlags.FireAndForget);
        }

        server.HotKeysStop();
        var result = server.HotKeysGet();
        Assert.NotNull(result);
        result.IsSlotFiltered.Should().BeTrue(nameof(result.IsSlotFiltered));
        var slots = result.SelectedSlots;
        slots.Length.Should().Be(1);
        slots[0].From.Should().Be(slot);
        slots[0].To.Should().Be(slot);

        Assert.SkipWhen(result.CpuByKey.IsEmpty, "Expected at least one CPU result"); // can be weird in CI
        bool found = false;
        foreach (var cpu in result.CpuByKey)
        {
            if (cpu.Key == key) found = true;
        }
        found.Should().BeTrue("key not found in CPU results");

        result.NetworkBytesByKey.IsEmpty.Should().BeFalse("Expected at least one network result");
        found = false;
        foreach (var net in result.NetworkBytesByKey)
        {
            if (net.Key == key) found = true;
        }
        found.Should().BeTrue("key not found in network results");

        (result.AllCommandSelectedSlotsMicroseconds >= 0).Should().BeTrue(nameof(result.AllCommandSelectedSlotsMicroseconds));
        (result.TotalCpuTimeUserMicroseconds >= 0).Should().BeTrue(nameof(result.TotalCpuTimeUserMicroseconds));

        result.IsSampled.Should().Be(sample);
        if (sample)
        {
            result.SampleRatio.Should().Be(3);
            (result.SampledCommandsSelectedSlotsMicroseconds >= 0).Should().BeTrue(nameof(result.SampledCommandsSelectedSlotsMicroseconds));
            (result.NetworkBytesSampledCommandsSelectedSlotsRaw >= 0).Should().BeTrue(nameof(result.NetworkBytesSampledCommandsSelectedSlotsRaw));
            result.SampledCommandsSelectedSlotsTime.HasValue.Should().BeTrue();
            result.SampledCommandsSelectedSlotsNetworkBytes.HasValue.Should().BeTrue();
        }
        else
        {
            result.SampleRatio.Should().Be(1);
            result.SampledCommandsSelectedSlotsMicroseconds.Should().Be(-1);
            result.NetworkBytesSampledCommandsSelectedSlotsRaw.Should().Be(-1);
            result.SampledCommandsSelectedSlotsTime.HasValue.Should().BeFalse();
            result.SampledCommandsSelectedSlotsNetworkBytes.HasValue.Should().BeFalse();
        }

        result.AllCommandsSelectedSlotsTime.HasValue.Should().BeTrue();
        result.AllCommandsSelectedSlotsNetworkBytes.HasValue.Should().BeTrue();
    }
}

[RunPerProtocol]
[Collection(NonParallelCollection.Name)]
public class HotKeysTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    protected TimeSpan Duration => TimeSpan.FromMinutes(1); // ensure we don't leave profiling running

    private protected IConnectionMultiplexer GetServer(out IServer server)
        => GetServer(RedisKey.Null, out server);

    private protected IConnectionMultiplexer GetServer(in RedisKey key, out IServer server)
    {
        var muxer = Create(require: RedisFeatures.v8_6_0, allowAdmin: true);
        server = key.IsNull ? muxer.GetServer(muxer.GetEndPoints()[0]) : muxer.GetServer(key);
        server.HotKeysStop(CommandFlags.FireAndForget);
        server.HotKeysReset(CommandFlags.FireAndForget);
        return muxer;
    }

    [Fact]
    public void get_when_empty_is_null()
    {
        using var muxer = GetServer(out var server);
        server.HotKeysGet().Should().BeNull();
    }

    [Fact]
    public async Task get_when_empty_is_null_async()
    {
        await using var muxer = GetServer(out var server);
        (await server.HotKeysGetAsync()).Should().BeNull();
    }

    [Fact]
    public void stop_when_not_running_is_false()
    {
        using var muxer = GetServer(out var server);
        server.HotKeysStop().Should().BeFalse();
    }

    [Fact]
    public async Task stop_when_not_running_is_false_async()
    {
        await using var muxer = GetServer(out var server);
        (await server.HotKeysStopAsync()).Should().BeFalse();
    }

    [Fact]
    public void can_start_stop_reset()
    {
        NoConcurrentRuntime();

        RedisKey key = Me();
        using var muxer = GetServer(key, out var server);
        server.HotKeysStart(duration: Duration);
        var db = muxer.GetDatabase();
        db.KeyDelete(key, flags: CommandFlags.FireAndForget);
        for (int i = 0; i < 20; i++)
        {
            db.StringIncrement(key, flags: CommandFlags.FireAndForget);
        }

        var result = server.HotKeysGet();
        Assert.NotNull(result);
        result.TrackingActive.Should().BeTrue();
        CheckSimpleWithKey(key, result, server);

        server.HotKeysStop().Should().BeTrue();
        result = server.HotKeysGet();
        Assert.NotNull(result);
        result.TrackingActive.Should().BeFalse();
        CheckSimpleWithKey(key, result, server);

        server.HotKeysReset();
        result = server.HotKeysGet();
        result.Should().BeNull();
    }

    private void CheckSimpleWithKey(RedisKey key, HotKeysResult hotKeys, IServer server)
    {
        hotKeys.Metrics.Should().Be(HotKeysMetrics.Cpu | HotKeysMetrics.Network);
        (hotKeys.CollectionDurationMicroseconds >= 0).Should().BeTrue(nameof(hotKeys.CollectionDurationMicroseconds));
        (hotKeys.CollectionStartTimeUnixMilliseconds >= 0).Should().BeTrue(nameof(hotKeys.CollectionStartTimeUnixMilliseconds));

        hotKeys.CpuByKey.IsEmpty.Should().BeFalse("Expected at least one CPU result");
        bool found = false;
        foreach (var cpu in hotKeys.CpuByKey)
        {
            (cpu.DurationMicroseconds >= 0).Should().BeTrue(nameof(cpu.DurationMicroseconds));
            if (cpu.Key == key) found = true;
        }
        found.Should().BeTrue("key not found in CPU results");

        hotKeys.NetworkBytesByKey.IsEmpty.Should().BeFalse("Expected at least one network result");
        found = false;
        foreach (var net in hotKeys.NetworkBytesByKey)
        {
            (net.Bytes > 0).Should().BeTrue(nameof(net.Bytes));
            if (net.Key == key) found = true;
        }
        found.Should().BeTrue("key not found in network results");

        hotKeys.SampleRatio.Should().Be(1);
        hotKeys.IsSampled.Should().BeFalse(nameof(hotKeys.IsSampled));
        hotKeys.IsSlotFiltered.Should().BeFalse(nameof(hotKeys.IsSlotFiltered));

        if (server.ServerType is ServerType.Cluster)
        {
            hotKeys.SelectedSlots.Length.Should().NotBe(0);
            Log("Cluster mode detected; not enforcing slots, but:");
            foreach (var slot in hotKeys.SelectedSlots)
            {
                Log($"  {slot}");
            }
        }
        else
        {
            hotKeys.SelectedSlots.Length.Should().Be(1);
            var slots = hotKeys.SelectedSlots[0];
            slots.From.Should().Be(SlotRange.MinSlot);
            slots.To.Should().Be(SlotRange.MaxSlot);
        }

        (hotKeys.AllCommandsAllSlotsMicroseconds >= 0).Should().BeTrue(nameof(hotKeys.AllCommandsAllSlotsMicroseconds));
        (hotKeys.TotalCpuTimeSystemMicroseconds >= 0).Should().BeTrue(nameof(hotKeys.TotalCpuTimeSystemMicroseconds));
        (hotKeys.TotalCpuTimeUserMicroseconds >= 0).Should().BeTrue(nameof(hotKeys.TotalCpuTimeUserMicroseconds));
        (hotKeys.AllCommandsAllSlotsNetworkBytes > 0).Should().BeTrue(nameof(hotKeys.AllCommandsAllSlotsNetworkBytes));
        (hotKeys.TotalNetworkBytes > 0).Should().BeTrue(nameof(hotKeys.TotalNetworkBytes));

        hotKeys.AllCommandsSelectedSlotsTime.HasValue.Should().BeFalse();
        hotKeys.AllCommandsSelectedSlotsNetworkBytes.HasValue.Should().BeFalse();
        hotKeys.SampledCommandsSelectedSlotsTime.HasValue.Should().BeFalse();
        hotKeys.SampledCommandsSelectedSlotsNetworkBytes.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task can_start_stop_reset_async()
    {
        NoConcurrentRuntime();

        RedisKey key = Me();
        await using var muxer = GetServer(key, out var server);
        await server.HotKeysStartAsync(duration: Duration);
        var db = muxer.GetDatabase();
        await db.KeyDeleteAsync(key, flags: CommandFlags.FireAndForget);
        for (int i = 0; i < 20; i++)
        {
            await db.StringIncrementAsync(key, flags: CommandFlags.FireAndForget);
        }

        var result = await server.HotKeysGetAsync();
        Assert.NotNull(result);
        result.TrackingActive.Should().BeTrue();
        CheckSimpleWithKey(key, result, server);

        (await server.HotKeysStopAsync()).Should().BeTrue();
        result = await server.HotKeysGetAsync();
        Assert.NotNull(result);
        result.TrackingActive.Should().BeFalse();
        CheckSimpleWithKey(key, result, server);

        await server.HotKeysResetAsync();
        result = await server.HotKeysGetAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task duration_filter_async()
    {
        NoConcurrentRuntime();

        Skip.UnlessLongRunning(); // time-based tests are horrible

        RedisKey key = Me();
        await using var muxer = GetServer(key, out var server);
        await server.HotKeysStartAsync(duration: TimeSpan.FromSeconds(1));
        var db = muxer.GetDatabase();
        await db.KeyDeleteAsync(key, flags: CommandFlags.FireAndForget);
        for (int i = 0; i < 20; i++)
        {
            await db.StringIncrementAsync(key, flags: CommandFlags.FireAndForget);
        }
        var before = await server.HotKeysGetAsync();
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        var after = await server.HotKeysGetAsync();

        Assert.NotNull(before);
        before.TrackingActive.Should().BeTrue();

        Assert.NotNull(after);
        after.TrackingActive.Should().BeFalse();

        var millis = after.CollectionDuration.TotalMilliseconds;
        Log($"Duration: {millis}ms");
        (millis > 900 && millis < 1100).Should().BeTrue();
    }

    [Theory]
    [InlineData(HotKeysMetrics.Cpu)]
    [InlineData(HotKeysMetrics.Network)]
    [InlineData(HotKeysMetrics.Network | HotKeysMetrics.Cpu)]
    public async Task metrics_choice_async(HotKeysMetrics metrics)
    {
        NoConcurrentRuntime();

        RedisKey key = Me();
        await using var muxer = GetServer(key, out var server);
        await server.HotKeysStartAsync(metrics, duration: Duration);
        var db = muxer.GetDatabase();
        await db.KeyDeleteAsync(key, flags: CommandFlags.FireAndForget);
        for (int i = 0; i < 20; i++)
        {
            await db.StringIncrementAsync(key, flags: CommandFlags.FireAndForget);
        }
        await server.HotKeysStopAsync(flags: CommandFlags.FireAndForget);
        var result = await server.HotKeysGetAsync();
        Assert.NotNull(result);
        result.Metrics.Should().Be(metrics);

        bool cpu = (metrics & HotKeysMetrics.Cpu) != 0;
        bool net = (metrics & HotKeysMetrics.Network) != 0;

        result.CpuByKey.IsEmpty.Should().NotBe(cpu);
        result.TotalCpuTimeSystem.HasValue.Should().Be(cpu);
        result.TotalCpuTimeUser.HasValue.Should().Be(cpu);
        result.TotalCpuTime.HasValue.Should().Be(cpu);

        result.NetworkBytesByKey.IsEmpty.Should().NotBe(net);
        result.TotalNetworkBytes.HasValue.Should().Be(net);
    }

    [Fact]
    public async Task sample_ratio_usage_async()
    {
        NoConcurrentRuntime();

        RedisKey key = Me();
        await using var muxer = GetServer(key, out var server);
        await server.HotKeysStartAsync(sampleRatio: 3, duration: Duration);
        var db = muxer.GetDatabase();
        await db.KeyDeleteAsync(key, flags: CommandFlags.FireAndForget);
        for (int i = 0; i < 20; i++)
        {
            await db.StringIncrementAsync(key, flags: CommandFlags.FireAndForget);
        }

        await server.HotKeysStopAsync(flags: CommandFlags.FireAndForget);
        var result = await server.HotKeysGetAsync();
        Assert.NotNull(result);
        result.IsSampled.Should().BeTrue(nameof(result.IsSampled));
        result.SampleRatio.Should().Be(3);
        result.TotalNetworkBytes.HasValue.Should().BeTrue();
        result.TotalCpuTime.HasValue.Should().BeTrue();
    }

    [Fact]
    public void non_negative_microseconds_converts_correctly()
    {
        // Test case: 103 microseconds should convert to 103 microseconds in TimeSpan
        // 103 microseconds = 103 * 10 ticks = 1030 ticks = 0.103 milliseconds
        long inputMicroseconds = 103;
        TimeSpan result = HotKeysResult.NonNegativeMicroseconds(inputMicroseconds);

        // Expected: 1030 ticks (103 microseconds = 0.103 milliseconds)
        result.Ticks.Should().Be(1030);
        result.TotalMilliseconds.Should().BeApproximately(0.103, 0.00000000005);
    }

    [Fact]
    public void non_negative_microseconds_handles_zero()
    {
        TimeSpan result = HotKeysResult.NonNegativeMicroseconds(0);
        result.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void non_negative_microseconds_handles_negative_as_zero()
    {
        TimeSpan result = HotKeysResult.NonNegativeMicroseconds(-100);
        result.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void non_negative_microseconds_handles_large_values()
    {
        // 1 second = 1,000,000 microseconds = 10,000,000 ticks = 1000 milliseconds
        long inputMicroseconds = 1_000_000;
        TimeSpan result = HotKeysResult.NonNegativeMicroseconds(inputMicroseconds);

        result.Ticks.Should().Be(10_000_000);
        result.TotalMilliseconds.Should().BeApproximately(1000.0, 0.00000000005);
        result.TotalSeconds.Should().BeApproximately(1.0, 0.00000000005);
    }
}

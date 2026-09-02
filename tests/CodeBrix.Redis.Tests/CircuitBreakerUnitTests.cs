using System;
using System.Threading;
using CodeBrix.Redis.Availability;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class CircuitBreakerUnitTests
{
    [Fact]
    public void builder_all_defaults_returns_shared_default_instance()
    {
        // a builder that hasn't been touched should collapse onto the shared default instance...
        var a = new CircuitBreaker.Builder().Create();
        var b = new CircuitBreaker.Builder().Create();

        b.Should().BeSameAs(a);
        a.Should().BeSameAs(CircuitBreaker.Default);
    }

    [Fact]
    public void builder_non_defaults_returns_distinct_valid_instances()
    {
        // ...but as soon as any knob is changed, we get a fresh, distinct instance per Create()
        CircuitBreaker.Builder Configured() => new() { FailureRateThreshold = 42 };

        var a = Configured().Create();
        var b = Configured().Create();

        a.Should().NotBeNull();
        b.Should().NotBeNull();
        b.Should().NotBeSameAs(a);
        a.Should().NotBeSameAs(CircuitBreaker.Default);
    }

    [Fact]
    public void none_is_distinct_from_default_but_stable()
    {
        CircuitBreaker.None.Should().NotBeNull();
        CircuitBreaker.None.Should().BeSameAs(CircuitBreaker.None);
        CircuitBreaker.None.Should().NotBeSameAs(CircuitBreaker.Default);
    }

    [Fact]
    public void none_is_always_healthy()
    {
        var acc = CircuitBreaker.None.CreateAccumulator();
        // even a solid wall of tracked failures never trips the no-op breaker
        Record(acc, 10_000, new RedisTimeoutException(CommandFlags.None, "boom", CommandStatus.Unknown)).Should().BeTrue();
    }

    // the time-windowed logic needs a controllable clock; TimeProvider is only available on net8.0+,
    // and we don't want to pull the BCL shim in just for down-level test coverage.
    [Fact]
    public void below_minimum_failures_stays_healthy()
    {
        var time = new ManualTimeProvider();
        // threshold is trivially low (1%), so the *only* thing keeping us healthy is the minimum-count gate
        var acc = Build(time, failureRateThreshold: 1, minimumNumberOfFailures: 10).CreateAccumulator();

        // nine tracked failures: one short of the minimum, so we withhold judgement
        Record(acc, 9, Timeout()).Should().BeTrue();
    }

    [Fact]
    public void above_threshold_trips()
    {
        var time = new ManualTimeProvider();
        var acc = Build(time, failureRateThreshold: 50, minimumNumberOfFailures: 10).CreateAccumulator();

        // 20 tracked failures, 0 successes -> 100% failure rate, well past both gates
        Record(acc, 20, Timeout()).Should().BeFalse();
    }

    [Fact]
    public void below_threshold_stays_healthy()
    {
        var time = new ManualTimeProvider();
        var acc = Build(time, failureRateThreshold: 50, minimumNumberOfFailures: 10).CreateAccumulator();

        Record(acc, 10, Timeout()); // enough failures to clear the minimum-count gate
        Record(acc, 190); // but drowned out by successes -> 5% failure rate

        // a pure health read confirms we're comfortably under the 50% threshold
        acc.IsHealthy().Should().BeTrue();
    }

    [Fact]
    public void untracked_exceptions_count_as_success()
    {
        var time = new ManualTimeProvider();
        // default tracking set (null) == RedisConnectionException + RedisTimeoutException only
        var acc = Build(time, failureRateThreshold: 1, minimumNumberOfFailures: 1).CreateAccumulator();

        // a flood of *untracked* failures must not trip the breaker...
        Record(acc, 100, new InvalidOperationException("not tracked")).Should().BeTrue();

        // ...whereas the same volume of tracked failures does
        Record(acc, 100, Timeout()).Should().BeFalse();
    }

    [Fact]
    public void old_failures_age_out_of_the_window()
    {
        var time = new ManualTimeProvider();
        var acc = Build(time, failureRateThreshold: 50, minimumNumberOfFailures: 1).CreateAccumulator();

        // saturate the window with failures -> tripped
        Record(acc, 100, Timeout()).Should().BeFalse();

        // step past the whole window; the earlier failures should no longer count
        time.Advance(TimeSpan.FromSeconds(11));

        // the window is now empty of in-range failures -> healthy again
        acc.IsHealthy().Should().BeTrue();
    }

    [Fact]
    public void is_healthy_reflects_state_without_observing()
    {
        var time = new ManualTimeProvider();
        var acc = Build(time, failureRateThreshold: 50, minimumNumberOfFailures: 1).CreateAccumulator();

        acc.IsHealthy().Should().BeTrue(); // nothing observed yet

        Record(acc, 100, Timeout()).Should().BeFalse(); // trip it via observations

        // the context-free overload reports the same verdict, purely by reading the window
        acc.IsHealthy().Should().BeFalse();
    }

    [Fact]
    public void reset_discards_history()
    {
        var time = new ManualTimeProvider();
        var acc = Build(time, failureRateThreshold: 50, minimumNumberOfFailures: 1).CreateAccumulator();

        // trip it wide open...
        Record(acc, 100, Timeout()).Should().BeFalse();

        // ...then wipe the slate; the prior failures are forgotten
        acc.Reset();

        // an empty window reads as healthy again
        acc.IsHealthy().Should().BeTrue();
    }

    private static CircuitBreaker Build(
        TimeProvider time,
        double failureRateThreshold,
        int minimumNumberOfFailures)
        => new CircuitBreaker.Builder
        {
            FailureRateThreshold = failureRateThreshold,
            MinimumNumberOfFailures = minimumNumberOfFailures,
            MetricsWindowSize = TimeSpan.FromSeconds(10),
            TimeProvider = time,
        }.Create();

    /// <summary>
    /// A hand-cranked <see cref="TimeProvider"/> whose clock only moves when we tell it to,
    /// so the bucketed metrics window is fully deterministic.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        // one tick == 100ns, matching TimeSpan; keeps Advance(TimeSpan) a straight addition
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Volatile.Read(ref _timestamp);

        public void Advance(TimeSpan by) => Interlocked.Add(ref _timestamp, by.Ticks);
    }

    private static RedisTimeoutException Timeout() => new(CommandFlags.None, "timeout", CommandStatus.Unknown);

    private static bool Record(CircuitBreaker.Accumulator accumulator, int count, Exception? fault = null)
    {
        // Trip applies the IsFailure gate (a fault the breaker doesn't consider a failure is counted as a
        // success), then records via ObserveResult; call it rather than ObserveResult directly so the gate runs
        for (int i = 0; i < count; i++)
        {
            accumulator.Trip(fault);
        }

        return accumulator.IsHealthy();
    }
}

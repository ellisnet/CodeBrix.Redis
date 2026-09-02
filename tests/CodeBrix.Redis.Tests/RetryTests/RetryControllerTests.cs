using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Redis.Availability;
using CodeBrix.Redis.Interfaces;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.RetryTests; //was previously: StackExchange.Redis.Tests.RetryTests;

// Configuration validation (which lives on RetryPolicy.Builder, since a RetryPolicy is immutable and
// validated on construction) and the wait/failover timing state machine of RetryController; neither needs a
// server, or even an inner database - CanRetry and the delays never touch one.
public class RetryControllerTests
{
    // A failover threshold below 1 could never be reached by the attempt counter (which starts at 1), so
    // it would *silently* disable failover; that is rejected up front.
    [Fact]
    public void policy_rejects_unreachable_failover_threshold()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy.Builder { MaxAttemptsBeforeFailover = 0 }.Create());

    // DatabaseFeatureFlags is internal, so theories take a bool and map here
    private static DatabaseFeatureFlags Features(bool withFailover)
        => withFailover ? DatabaseFeatureFlags.Failover : DatabaseFeatureFlags.None;

    // Negative durations are nonsense for a delay; each is validated separately.
    [Fact]
    public void policy_rejects_negative_durations()
    {
        var negative = TimeSpan.FromMilliseconds(-1);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy.Builder { RetryDelay = negative }.Create());
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy.Builder { JitterMax = negative }.Create());
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy.Builder { FailoverDelay = negative }.Create());
    }

    // The watch-contention budget counts *attempts*, so 1 means "try once, do not re-attempt"; zero or
    // negative is meaningless rather than a way to say "never execute".
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void policy_rejects_non_positive_watch_attempts(int attempts)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy.Builder { MaxAttemptsOnWatchConflict = attempts }.Create());

    // ...and 1 is accepted, since that is how re-attempting is switched off
    [Fact]
    public void policy_accepts_single_watch_attempt()
        => new RetryPolicy.Builder { MaxAttemptsOnWatchConflict = 1 }.Create().MaxAttemptsOnWatchConflict.Should().Be(1);

    // The category cap must name exactly one of the CommandRetry* values: an empty value, or one that
    // strays outside the category bits, is a usage error rather than something to interpret.
    [Fact]
    public void policy_rejects_non_category_max_command_retry_category()
    {
        Assert.Throws<ArgumentException>(() => new RetryPolicy.Builder { MaxCommandRetryCategory = CommandFlags.None }.Create());
        Assert.Throws<ArgumentException>(() => new RetryPolicy.Builder { MaxCommandRetryCategory = CommandFlags.FireAndForget }.Create());
        Assert.Throws<ArgumentException>(
            () => new RetryPolicy.Builder { MaxCommandRetryCategory = CommandFlags.CommandRetryReadOnly | CommandFlags.FireAndForget }.Create());

        RetryPolicy valid = new RetryPolicy.Builder { MaxCommandRetryCategory = CommandFlags.CommandRetryAlways };
        valid.MaxCommandRetryCategory.Should().Be(CommandFlags.CommandRetryAlways);
    }

    // RetryPolicy.None means *nothing* is re-attempted - including watch contention, which is bounded by
    // an attempt count rather than by CanRetry (nothing was applied, so there is no fault to judge).
    [Fact]
    public void none_policy_disables_watch_reattempts()
    {
        RetryPolicy.None.MaxAttemptsOnWatchConflict.Should().Be(1);
        RetryPolicy.Default.MaxAttemptsOnWatchConflict.Should().Be(DefaultMaxAttemptsOnWatchConflict);
    }

    private const int DefaultMaxAttemptsOnWatchConflict = 3;

    // Round-tripping a policy through a builder must preserve the watch budget along with everything else.
    [Fact]
    public void policy_round_trips_through_builder()
    {
        RetryPolicy original = new RetryPolicy.Builder { MaxAttemptsOnWatchConflict = 7, MaxAttempts = 4 };
        var copy = new RetryPolicy.Builder(original).Create();

        copy.MaxAttemptsOnWatchConflict.Should().Be(7);
        copy.MaxAttempts.Should().Be(4);
    }

    // Contention is not a fault, so there is no backoff - only jitter, to stop two callers colliding again
    // in lock-step. With jitter disabled the re-attempt is immediate.
    [Fact]
    public async Task watch_conflict_delay_has_no_backoff()
    {
        var controller = new RetryController(
            new RetryPolicy.Builder
            {
                RetryDelay = TimeSpan.FromMilliseconds(LongMillis),
                FailoverDelay = TimeSpan.FromMilliseconds(LongMillis),
                JitterMax = TimeSpan.Zero,
            },
            DatabaseFeatureFlags.Failover);

        var watch = Stopwatch.StartNew();
        await controller.WatchConflictDelayAsync();
        (watch.ElapsedMilliseconds < ShortMillis).Should().BeTrue($"returned after {watch.ElapsedMilliseconds}ms");
    }

    // Capturing the "next failover" token costs something, so we only do it when a failover could
    // actually be waited on: the database must offer failover, there must be more than one attempt, and
    // the threshold must sit strictly below the attempt cap (at the cap it can never be reached).
    [Theory]
    [InlineData(3, 1, true, true)]
    [InlineData(3, 1, false, false)] // no failover available
    [InlineData(1, 1, true, false)] // single attempt: nothing to retry
    [InlineData(3, 3, true, false)] // threshold == cap: unreachable
    [InlineData(3, 4, true, false)] // threshold beyond cap: unreachable
    public void tracks_failover_only_when_reachable(int maxAttempts, int beforeFailover, bool withFailover, bool expected)
    {
        RetryPolicy policy = new RetryPolicy.Builder { MaxAttempts = maxAttempts, MaxAttemptsBeforeFailover = beforeFailover };
        new RetryController(policy, Features(withFailover)).TracksFailover.Should().Be(expected);
    }

    // MaxAttempts = 1 means "try once": the very first failure is already exhausted.
    [Fact]
    public void single_attempt_never_retries()
    {
        var controller = new RetryController(new RetryPolicy.Builder { MaxAttempts = 1 }, DatabaseFeatureFlags.Failover);
        using var cts = new CancellationTokenSource();
        var failover = cts.Token;
        var fault = new RedisServerException(RedisErrorKind.Loading, CommandFlags.CommandRetryReadOnly, "LOADING");

        controller.CanRetry(1, fault, ref failover, out var delay).Should().BeFalse();
        delay.CanBeCanceled.Should().BeFalse();
    }

    // --- FailoverOrDelayAsync -----------------------------------------------------------------------
    // Deliberately coarse thresholds: we are distinguishing "waited for the configured period" from
    // "returned as soon as it could", not measuring the clock.
    private const int LongMillis = 2000, ShortMillis = 1000;

    // No failover token: this is a routine pause between same-server attempts, so it waits RetryDelay.
    [Fact]
    public async Task delay_without_failover_token_waits_retry_delay()
    {
        var controller = new RetryController(
            new RetryPolicy.Builder { RetryDelay = TimeSpan.FromMilliseconds(LongMillis), JitterMax = TimeSpan.Zero },
            DatabaseFeatureFlags.None);

        var watch = Stopwatch.StartNew();
        await controller.FailoverOrDelayAsync(CancellationToken.None);
        (watch.ElapsedMilliseconds >= ShortMillis).Should().BeTrue($"returned after {watch.ElapsedMilliseconds}ms");
    }

    // A failover token that has *already* fired: there is nothing to wait for, so only jitter applies -
    // and in particular RetryDelay is deliberately ignored on the failover path.
    [Fact]
    public async Task delay_with_fired_failover_token_returns_immediately()
    {
        var controller = new RetryController(
            new RetryPolicy.Builder
            {
                RetryDelay = TimeSpan.FromMilliseconds(LongMillis),
                FailoverDelay = TimeSpan.FromMilliseconds(LongMillis),
                JitterMax = TimeSpan.Zero,
            },
            DatabaseFeatureFlags.Failover);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel, not CancelAsync: this project also targets net481

        var watch = Stopwatch.StartNew();
        await controller.FailoverOrDelayAsync(cts.Token);
        (watch.ElapsedMilliseconds < ShortMillis).Should().BeTrue($"returned after {watch.ElapsedMilliseconds}ms");
    }

    // A failover that arrives while we are waiting: we stop waiting as soon as it lands, rather than
    // sitting out the whole FailoverDelay.
    [Fact]
    public async Task delay_when_failover_arrives_stops_waiting()
    {
        var controller = new RetryController(
            new RetryPolicy.Builder { FailoverDelay = TimeSpan.FromMilliseconds(LongMillis * 4), JitterMax = TimeSpan.Zero },
            DatabaseFeatureFlags.Failover);

        using var cts = new CancellationTokenSource();
        var watch = Stopwatch.StartNew();
        var pending = controller.FailoverOrDelayAsync(cts.Token);
        cts.Cancel(); // Cancel, not CancelAsync: this project also targets net481
        await pending;

        (watch.ElapsedMilliseconds < ShortMillis).Should().BeTrue($"returned after {watch.ElapsedMilliseconds}ms");
    }

    // A failover that never arrives: we give it FailoverDelay and then proceed anyway (retrying on the
    // original server is better than giving up).
    [Fact]
    public async Task delay_when_failover_never_arrives_proceeds_after_failover_delay()
    {
        var controller = new RetryController(
            new RetryPolicy.Builder { FailoverDelay = TimeSpan.FromMilliseconds(LongMillis), JitterMax = TimeSpan.Zero },
            DatabaseFeatureFlags.Failover);

        using var cts = new CancellationTokenSource();
        var watch = Stopwatch.StartNew();
        await controller.FailoverOrDelayAsync(cts.Token);

        (watch.ElapsedMilliseconds >= ShortMillis).Should().BeTrue($"returned after {watch.ElapsedMilliseconds}ms");
        cts.IsCancellationRequested.Should().BeFalse(); // no failover ever happened
    }
}

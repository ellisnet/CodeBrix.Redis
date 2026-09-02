using System;
using System.Threading.Tasks;
using CodeBrix.Redis.Availability;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// Covers the shape shared by every Availability configuration type: an immutable policy with static
/// Default/None, configured through a nested Builder that validates in Create() and collapses onto the
/// shared default when nothing was customized.
/// </summary>
public class AvailabilityConfigTests
{
    // ---- HealthCheck ----
    [Fact]
    public void health_check_untouched_builder_collapses_onto_default()
    {
        (new HealthCheck.Builder().Create()).Should().BeSameAs(HealthCheck.Default);
        (new HealthCheck.Builder(HealthCheck.Default).Create()).Should().BeSameAs(HealthCheck.Default);
    }

    [Fact]
    public void health_check_builder_round_trips_existing_instance()
    {
        HealthCheck original = new HealthCheck.Builder
        {
            ProbeCount = 7,
            ProbeTimeout = TimeSpan.FromSeconds(11),
            ProbeInterval = TimeSpan.FromMilliseconds(250),
            Probe = HealthCheckProbe.IsConnected,
            ProbePolicy = HealthCheckProbePolicy.MajoritySuccess,
        };

        // the copy constructor is the replacement for the old Clone()
        var copy = new HealthCheck.Builder(original).Create();

        copy.Should().NotBeSameAs(original);
        copy.ProbeCount.Should().Be(original.ProbeCount);
        copy.ProbeTimeout.Should().Be(original.ProbeTimeout);
        copy.ProbeInterval.Should().Be(original.ProbeInterval);
        copy.Probe.Should().BeSameAs(original.Probe);
        copy.ProbePolicy.Should().BeSameAs(original.ProbePolicy);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void health_check_rejects_non_positive_probe_count(int probeCount)
    {
        var builder = new HealthCheck.Builder { ProbeCount = probeCount };
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => builder.Create());
        ex.ParamName.Should().Be(nameof(HealthCheck.Builder.ProbeCount));
    }

    [Fact]
    public void health_check_rejects_non_positive_probe_timeout()
    {
        var builder = new HealthCheck.Builder { ProbeTimeout = TimeSpan.Zero };
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => builder.Create());
        ex.ParamName.Should().Be(nameof(HealthCheck.Builder.ProbeTimeout));
    }

    [Fact]
    public void health_check_rejects_negative_probe_interval()
    {
        var builder = new HealthCheck.Builder { ProbeInterval = TimeSpan.FromMilliseconds(-1) };
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => builder.Create());
        ex.ParamName.Should().Be(nameof(HealthCheck.Builder.ProbeInterval));
    }

    [Fact]
    public void health_check_rejects_unrepresentable_total_budget()
    {
        // ProbeCount x ProbeTimeout has to fit in int milliseconds; this used to overflow silently
        var builder = new HealthCheck.Builder { ProbeCount = 1000, ProbeTimeout = TimeSpan.FromDays(30) };
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Create());
    }

    [Fact]
    public void health_check_none_is_disabled_and_stable()
    {
        HealthCheck.None.Should().BeSameAs(HealthCheck.None);
        HealthCheck.Default.Should().NotBeSameAs(HealthCheck.None);
        HealthCheck.None.IsEnabled.Should().BeFalse();
        HealthCheck.Default.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task health_check_none_reports_inconclusive_without_probing()
    {
        // a null server would throw if the probe were actually invoked
        (await HealthCheck.None.CheckHealthAsync(server: null!)).Should().Be(HealthCheckResult.Inconclusive);
    }

    // ---- RetryPolicy ----
    [Fact]
    public void retry_policy_untouched_builder_collapses_onto_default()
    {
        (new RetryPolicy.Builder().Create()).Should().BeSameAs(RetryPolicy.Default);
        (new RetryPolicy.Builder(RetryPolicy.Default).Create()).Should().BeSameAs(RetryPolicy.Default);
    }

    [Fact]
    public void retry_policy_builder_round_trips_existing_instance()
    {
        RetryPolicy original = new RetryPolicy.Builder
        {
            MaxAttempts = 9,
            MaxAttemptsBeforeFailover = 4,
            RetryDelay = TimeSpan.FromMilliseconds(123),
            JitterMax = TimeSpan.FromMilliseconds(45),
            FailoverDelay = TimeSpan.FromSeconds(6),
            MaxCommandRetryCategory = CommandFlags.CommandRetryWriteAccumulating,
        };

        var copy = new RetryPolicy.Builder(original).Create();

        copy.Should().NotBeSameAs(original);
        copy.MaxAttempts.Should().Be(original.MaxAttempts);
        copy.MaxAttemptsBeforeFailover.Should().Be(original.MaxAttemptsBeforeFailover);
        copy.RetryDelay.Should().Be(original.RetryDelay);
        copy.JitterMax.Should().Be(original.JitterMax);
        copy.FailoverDelay.Should().Be(original.FailoverDelay);
        copy.MaxCommandRetryCategory.Should().Be(original.MaxCommandRetryCategory);
    }

    [Fact]
    public void retry_policy_rejects_zero_attempts()
    {
        var builder = new RetryPolicy.Builder { MaxAttempts = 0 };
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => builder.Create());
        ex.ParamName.Should().Be(nameof(RetryPolicy.Builder.MaxAttempts));
    }

    [Fact]
    public void retry_policy_rejects_zero_attempts_before_failover()
    {
        // previously this silently disabled failover, and only threw later, from WithRetry
        var builder = new RetryPolicy.Builder { MaxAttemptsBeforeFailover = 0 };
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => builder.Create());
        ex.ParamName.Should().Be(nameof(RetryPolicy.Builder.MaxAttemptsBeforeFailover));
    }

    [Fact]
    public void retry_policy_rejects_negative_delays()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy.Builder { RetryDelay = TimeSpan.FromTicks(-1) }.Create()).ParamName.Should().Be(nameof(RetryPolicy.Builder.RetryDelay));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy.Builder { JitterMax = TimeSpan.FromTicks(-1) }.Create()).ParamName.Should().Be(nameof(RetryPolicy.Builder.JitterMax));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy.Builder { FailoverDelay = TimeSpan.FromTicks(-1) }.Create()).ParamName.Should().Be(nameof(RetryPolicy.Builder.FailoverDelay));
    }

    [Theory]
    [InlineData(CommandFlags.None)] // no category at all
    [InlineData(CommandFlags.FireAndForget)] // not a category
    [InlineData(CommandFlags.CommandRetryReadOnly | CommandFlags.PreferReplica)] // category plus noise
    public void retry_policy_rejects_invalid_retry_category(CommandFlags flags)
    {
        var builder = new RetryPolicy.Builder { MaxCommandRetryCategory = flags };
        var ex = Assert.Throws<ArgumentException>(() => builder.Create());
        ex.ParamName.Should().Be(nameof(RetryPolicy.Builder.MaxCommandRetryCategory));
    }

    [Fact]
    public void retry_policy_none_never_retries()
    {
        RetryPolicy.None.Should().BeSameAs(RetryPolicy.None);
        RetryPolicy.Default.Should().NotBeSameAs(RetryPolicy.None);

        // a transient, retryable fault on a read-only command: the default policy retries, None does not
        var fault = new FaultContext(new RedisConnectionException(ConnectionFailureType.SocketFailure, CommandFlags.None, "boom"));
        RetryPolicy.None.CanRetry(in fault).Should().Be(RetryResult.None);
    }

    // ---- CircuitBreaker ----
    [Fact]
    public void circuit_breaker_rejects_out_of_range_threshold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitBreaker.Builder { FailureRateThreshold = 101 }.Create()).ParamName.Should().Be(nameof(CircuitBreaker.Builder.FailureRateThreshold));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitBreaker.Builder { FailureRateThreshold = -1 }.Create()).ParamName.Should().Be(nameof(CircuitBreaker.Builder.FailureRateThreshold));
    }

    [Fact]
    public void circuit_breaker_rejects_invalid_window_and_minimum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitBreaker.Builder { MinimumNumberOfFailures = 0 }.Create()).ParamName.Should().Be(nameof(CircuitBreaker.Builder.MinimumNumberOfFailures));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitBreaker.Builder { MetricsWindowSize = TimeSpan.Zero }.Create()).ParamName.Should().Be(nameof(CircuitBreaker.Builder.MetricsWindowSize));
    }

    // ---- MultiGroupOptions ----
    [Fact]
    public void multi_group_options_untouched_builder_collapses_onto_default()
    {
        (new MultiGroupOptions.Builder().Create()).Should().BeSameAs(MultiGroupOptions.Default);
        (new MultiGroupOptions.Builder(MultiGroupOptions.Default).Create()).Should().BeSameAs(MultiGroupOptions.Default);
    }

    [Fact]
    public void multi_group_options_defaults_are_the_shared_policy_defaults()
    {
        var options = MultiGroupOptions.Default;
        options.HealthCheck.Should().BeSameAs(HealthCheck.Default);
        options.CircuitBreaker.Should().BeSameAs(CircuitBreaker.Default);
        options.RetryPolicy.Should().BeSameAs(RetryPolicy.Default);
        options.HealthCheckInterval.Should().Be(TimeSpan.FromSeconds(5));
        options.FailbackDelay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void multi_group_options_rejects_invalid_intervals()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiGroupOptions.Builder { HealthCheckInterval = TimeSpan.Zero }.Create()).ParamName.Should().Be(nameof(MultiGroupOptions.Builder.HealthCheckInterval));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiGroupOptions.Builder { FailbackDelay = TimeSpan.FromTicks(-1) }.Create()).ParamName.Should().Be(nameof(MultiGroupOptions.Builder.FailbackDelay));

        // MaxValue is the documented "never" sentinel for both, and must remain legal
        MultiGroupOptions ok = new MultiGroupOptions.Builder
        {
            HealthCheckInterval = TimeSpan.MaxValue,
            FailbackDelay = TimeSpan.MaxValue,
        };
        ok.HealthCheckInterval.Should().Be(TimeSpan.MaxValue);
        ok.FailbackDelay.Should().Be(TimeSpan.MaxValue);
    }

    [Fact]
    public void multi_group_options_builder_converts_implicitly()
    {
        // every Builder in the namespace supports this, so options can be written inline at the call-site
        MultiGroupOptions options = new MultiGroupOptions.Builder { FailbackDelay = TimeSpan.FromMinutes(2) };
        options.FailbackDelay.Should().Be(TimeSpan.FromMinutes(2));
    }

    // ---- per-member override resolution ----
    [Fact]
    public void member_resolves_group_defaults_when_no_override()
    {
        //Arrange
        var member = new ConnectionGroupMember("localhost:6379");

        //Act
        var options = MultiGroupOptions.Default;

        //Assert
        member.ResolveHealthCheck(options).Should().BeSameAs(options.HealthCheck);
        member.ResolveCircuitBreaker(options).Should().BeSameAs(options.CircuitBreaker);
        member.ResolveFailbackDelay(options).Should().Be(options.FailbackDelay);
    }

    [Fact]
    public void member_overrides_beat_group_defaults()
    {
        HealthCheck memberCheck = new HealthCheck.Builder { ProbeCount = 1 };
        CircuitBreaker memberBreaker = new CircuitBreaker.Builder { FailureRateThreshold = 42 };
        var member = new ConnectionGroupMember("localhost:6379")
        {
            HealthCheck = memberCheck,
            CircuitBreaker = memberBreaker,
            FailbackDelay = TimeSpan.FromMinutes(3),
        };

        var options = MultiGroupOptions.Default;
        member.ResolveHealthCheck(options).Should().BeSameAs(memberCheck);
        member.ResolveCircuitBreaker(options).Should().BeSameAs(memberBreaker);
        member.ResolveFailbackDelay(options).Should().Be(TimeSpan.FromMinutes(3));
    }

    [Fact]
    public void member_circuit_breaker_falls_back_to_its_own_configuration_before_the_group()
    {
        // precedence is: member override, then the member's own ConfigurationOptions, then the group default
        CircuitBreaker fromConfig = new CircuitBreaker.Builder { FailureRateThreshold = 42 };
        var config = ConfigurationOptions.Parse("localhost:6379");
        config.CircuitBreaker = fromConfig;

        var member = new ConnectionGroupMember(config);
        member.ResolveCircuitBreaker(MultiGroupOptions.Default).Should().BeSameAs(fromConfig);

        CircuitBreaker fromMember = new CircuitBreaker.Builder { FailureRateThreshold = 13 };
        member.CircuitBreaker = fromMember;
        member.ResolveCircuitBreaker(MultiGroupOptions.Default).Should().BeSameAs(fromMember);
    }

    [Fact]
    public void group_defaults_are_not_written_back_into_caller_configuration()
    {
        // callers may legitimately reuse a ConfigurationOptions across connections, so resolving a group
        // default must not mutate it (this used to be a `config.CircuitBreaker ??= options.CircuitBreaker`)
        var config = ConfigurationOptions.Parse("localhost:6379");
        var member = new ConnectionGroupMember(config);

        member.ResolveCircuitBreaker(MultiGroupOptions.Default).Should().BeSameAs(MultiGroupOptions.Default.CircuitBreaker);
        config.CircuitBreaker.Should().BeNull();
    }

    // ---- WithRetry() policy resolution ----
    [Fact]
    public async Task with_retry_uses_configured_policy_for_a_single_connection()
    {
        RetryPolicy configured = new RetryPolicy.Builder { MaxAttempts = 7 };
        var config = ConfigurationOptions.Parse("localhost:6379");
        config.RetryPolicy = configured;
        config.AbortOnConnectFail = false;

        await using var muxer = await ConnectionMultiplexer.ConnectAsync(config);
        var retrying = Assert.IsType<RetryDatabase>(muxer.GetDatabase().WithRetry());
        retrying.Policy.Should().BeSameAs(configured);
    }

    [Fact]
    public async Task with_retry_falls_back_to_default_when_none_configured()
    {
        //Arrange
        var config = ConfigurationOptions.Parse("localhost:6379");
        config.AbortOnConnectFail = false;
        await using var muxer = await ConnectionMultiplexer.ConnectAsync(config);

        //Act
        var retrying = Assert.IsType<RetryDatabase>(muxer.GetDatabase().WithRetry());

        //Assert
        retrying.Policy.Should().BeSameAs(RetryPolicy.Default);
    }
}

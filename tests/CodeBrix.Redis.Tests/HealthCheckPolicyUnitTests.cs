using System;
using CodeBrix.Redis.Availability;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class HealthCheckPolicyUnitTests
{
    [Theory]
    [InlineData(0, 0, 5, HealthCheckResult.Inconclusive)] // No results yet
    [InlineData(1, 0, 0, HealthCheckResult.Healthy)] // One success, no more probes
    [InlineData(0, 1, 0, HealthCheckResult.Unhealthy)] // One failure, no more probes
    [InlineData(2, 1, 0, HealthCheckResult.Healthy)] // Mixed results, success wins
    [InlineData(1, 2, 0, HealthCheckResult.Healthy)] // Mixed results, success wins
    [InlineData(5, 0, 0, HealthCheckResult.Healthy)] // All successes
    [InlineData(0, 5, 0, HealthCheckResult.Unhealthy)] // All failures
    [InlineData(1, 0, 2, HealthCheckResult.Healthy)] // Early success
    [InlineData(0, 1, 2, HealthCheckResult.Inconclusive)] // Early failure but more probes remain
    public void any_success_evaluates_correctly(int success, int failure, int remaining, HealthCheckResult expected)
    {
        //Arrange
        var policy = HealthCheckProbePolicy.AnySuccess;
        var context = new HealthCheckProbeContext(HealthCheckResult.Inconclusive, success, failure, remaining, TimeSpan.Zero);

        //Act
        var result = policy.Evaluate(context);

        //Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 0, 5, HealthCheckResult.Inconclusive)] // No results yet
    [InlineData(1, 0, 0, HealthCheckResult.Healthy)] // One success, no more probes
    [InlineData(0, 1, 0, HealthCheckResult.Unhealthy)] // One failure, no more probes
    [InlineData(2, 1, 0, HealthCheckResult.Unhealthy)] // Mixed results, one failure is enough
    [InlineData(1, 2, 0, HealthCheckResult.Unhealthy)] // Mixed results, one failure is enough
    [InlineData(5, 0, 0, HealthCheckResult.Healthy)] // All successes
    [InlineData(0, 5, 0, HealthCheckResult.Unhealthy)] // All failures
    [InlineData(1, 0, 2, HealthCheckResult.Inconclusive)] // Success but more probes remain
    [InlineData(0, 1, 2, HealthCheckResult.Unhealthy)] // Early failure
    [InlineData(4, 0, 1, HealthCheckResult.Inconclusive)] // Multiple successes but still waiting
    public void all_success_evaluates_correctly(int success, int failure, int remaining, HealthCheckResult expected)
    {
        //Arrange
        var policy = HealthCheckProbePolicy.AllSuccess;
        var context = new HealthCheckProbeContext(HealthCheckResult.Inconclusive, success, failure, remaining, TimeSpan.Zero);

        //Act
        var result = policy.Evaluate(context);

        //Assert
        result.Should().Be(expected);
    }

    [Theory]
    // Total 5 probes: need 3 for majority
    [InlineData(0, 0, 5, HealthCheckResult.Inconclusive)] // No results yet
    [InlineData(3, 0, 2, HealthCheckResult.Healthy)] // Reached majority (3/5)
    [InlineData(2, 0, 3, HealthCheckResult.Inconclusive)] // Not yet majority
    [InlineData(0, 3, 2, HealthCheckResult.Unhealthy)] // Majority impossible (3 failures)
    [InlineData(2, 2, 1, HealthCheckResult.Inconclusive)] // Tied, one more probe
    [InlineData(3, 2, 0, HealthCheckResult.Healthy)] // Majority achieved (3/5)
    [InlineData(2, 3, 0, HealthCheckResult.Unhealthy)] // Majority failed (3/5)
    [InlineData(5, 0, 0, HealthCheckResult.Healthy)] // All successes
    [InlineData(0, 5, 0, HealthCheckResult.Unhealthy)] // All failures

    // Total 3 probes: need 2 for majority
    [InlineData(0, 0, 3, HealthCheckResult.Inconclusive)] // No results yet (3 total)
    [InlineData(2, 0, 1, HealthCheckResult.Healthy)] // Reached majority (2/3)
    [InlineData(1, 0, 2, HealthCheckResult.Inconclusive)] // Not yet majority (3 total)
    [InlineData(0, 2, 1, HealthCheckResult.Unhealthy)] // Majority impossible (2 failures of 3)
    [InlineData(2, 1, 0, HealthCheckResult.Healthy)] // Majority achieved (2/3)
    [InlineData(1, 2, 0, HealthCheckResult.Unhealthy)] // Majority failed (2/3)

    // Total 1 probe: need 1 for majority
    [InlineData(0, 0, 1, HealthCheckResult.Inconclusive)] // No results yet (1 total)
    [InlineData(1, 0, 0, HealthCheckResult.Healthy)] // Majority achieved (1/1)
    [InlineData(0, 1, 0, HealthCheckResult.Unhealthy)] // Majority failed (1/1)

    // Total 6 probes: need 4 for majority
    [InlineData(4, 0, 2, HealthCheckResult.Healthy)] // Reached majority (4/6)
    [InlineData(3, 0, 3, HealthCheckResult.Inconclusive)] // Not yet majority (6 total)
    [InlineData(0, 4, 2, HealthCheckResult.Unhealthy)] // Majority impossible (4 failures)
    [InlineData(3, 3, 0, HealthCheckResult.Inconclusive)] // Tied, neither side has majority (3/6 is not >=4)
    public void majority_success_evaluates_correctly(int success, int failure, int remaining, HealthCheckResult expected)
    {
        var policy = HealthCheckProbePolicy.MajoritySuccess;
        var context = new HealthCheckProbeContext(HealthCheckResult.Inconclusive, success, failure, remaining, TimeSpan.Zero);

        var result = policy.Evaluate(context);

        result.Should().Be(expected);
    }

    [Fact]
    public void policies_are_singletons()
    {
        var any1 = HealthCheckProbePolicy.AnySuccess;
        var any2 = HealthCheckProbePolicy.AnySuccess;
        any1.Should().NotBeNull();
        any2.Should().BeSameAs(any1);

        var all1 = HealthCheckProbePolicy.AllSuccess;
        var all2 = HealthCheckProbePolicy.AllSuccess;
        all1.Should().NotBeNull();
        all2.Should().BeSameAs(all1);

        var maj1 = HealthCheckProbePolicy.MajoritySuccess;
        var maj2 = HealthCheckProbePolicy.MajoritySuccess;
        maj1.Should().NotBeNull();
        maj2.Should().BeSameAs(maj1);
    }
}

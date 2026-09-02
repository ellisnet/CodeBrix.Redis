using System;
using System.Net;
using System.Threading.Tasks;
using CodeBrix.Redis.Availability;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.MultiGroupTests; //was previously: StackExchange.Redis.Tests.MultiGroupTests;

/// <summary>
/// Verifies how a live group resolves its configuration: that group defaults reach the members, that a
/// per-member override wins, and that none of this mutates the caller's <see cref="ConfigurationOptions"/>.
/// </summary>
public class GroupConfigResolutionTests(ITestOutputHelper log)
{
    [Fact]
    public async Task group_exposes_its_options()
    {
        using var server0 = new InProcessTestServer(log, endpoint: new DnsEndPoint("alpha", 6379));
        using var server1 = new InProcessTestServer(log, endpoint: new DnsEndPoint("beta", 6379));

        MultiGroupOptions options = new MultiGroupOptions.Builder
        {
            FailbackDelay = TimeSpan.FromMinutes(4),
            RetryPolicy = new RetryPolicy.Builder { MaxAttempts = 6 },
        };

        ConnectionGroupMember[] members = [new(server0.GetClientConfig()), new(server1.GetClientConfig())];
        await using var conn = await ConnectionMultiplexer.ConnectGroupAsync(members, options);

        conn.Options.Should().BeSameAs(options);
        conn.Options.FailbackDelay.Should().Be(TimeSpan.FromMinutes(4));
    }

    [Fact]
    public async Task with_retry_uses_the_group_policy()
    {
        using var server0 = new InProcessTestServer(log, endpoint: new DnsEndPoint("alpha", 6379));
        using var server1 = new InProcessTestServer(log, endpoint: new DnsEndPoint("beta", 6379));

        RetryPolicy groupPolicy = new RetryPolicy.Builder { MaxAttempts = 6, RetryDelay = TimeSpan.Zero };
        MultiGroupOptions options = new MultiGroupOptions.Builder { RetryPolicy = groupPolicy };

        ConnectionGroupMember[] members = [new(server0.GetClientConfig()), new(server1.GetClientConfig())];
        await using var conn = await ConnectionMultiplexer.ConnectGroupAsync(members, options);

        // the parameterless overload resolves the policy from the group it is attached to
        var retrying = Assert.IsType<RetryDatabase>(conn.GetDatabase().WithRetry());
        retrying.Policy.Should().BeSameAs(groupPolicy);

        // ...and an explicit policy still wins
        RetryPolicy explicitPolicy = new RetryPolicy.Builder { MaxAttempts = 2 };
        var explicitlyRetrying = Assert.IsType<RetryDatabase>(conn.GetDatabase().WithRetry(explicitPolicy));
        explicitlyRetrying.Policy.Should().BeSameAs(explicitPolicy);
    }

    [Fact]
    public async Task group_circuit_breaker_reaches_members_without_mutating_caller_config()
    {
        using var server0 = new InProcessTestServer(log, endpoint: new DnsEndPoint("alpha", 6379));
        using var server1 = new InProcessTestServer(log, endpoint: new DnsEndPoint("beta", 6379));

        CircuitBreaker groupBreaker = new CircuitBreaker.Builder { FailureRateThreshold = 42 };
        CircuitBreaker memberBreaker = new CircuitBreaker.Builder { FailureRateThreshold = 13 };

        var config0 = server0.GetClientConfig();
        var config1 = server1.GetClientConfig();

        MultiGroupOptions options = new MultiGroupOptions.Builder { CircuitBreaker = groupBreaker };
        ConnectionGroupMember[] members = [new(config0), new(config1) { CircuitBreaker = memberBreaker }];
        await using var conn = await ConnectionMultiplexer.ConnectGroupAsync(members, options);

        // the group default reached the first member's connection, and the override reached the second
        AsMultiplexer(members[0]).EffectiveCircuitBreaker.Should().BeSameAs(groupBreaker);
        AsMultiplexer(members[1]).EffectiveCircuitBreaker.Should().BeSameAs(memberBreaker);

        // ...and neither was written back into the caller's configuration, which remains reusable
        config0.CircuitBreaker.Should().BeNull();
        config1.CircuitBreaker.Should().BeNull();

        static ConnectionMultiplexer AsMultiplexer(ConnectionGroupMember member) => member.Multiplexer;
    }

    [Fact]
    public async Task disabled_health_check_leaves_member_selectable_on_connectivity_alone()
    {
        using var server0 = new InProcessTestServer(log, endpoint: new DnsEndPoint("alpha", 6379));
        using var server1 = new InProcessTestServer(log, endpoint: new DnsEndPoint("beta", 6379));

        // HealthCheck.None performs no probes and reports Inconclusive, which is not Unhealthy - so a
        // connected member stays eligible, and the higher weight still wins
        MultiGroupOptions options = new MultiGroupOptions.Builder { HealthCheck = HealthCheck.None };
        ConnectionGroupMember[] members = [
            new(server0.GetClientConfig(), "alpha") { Weight = 1 },
            new(server1.GetClientConfig(), "beta") { Weight = 9 },
        ];

        await using var conn = await ConnectionMultiplexer.ConnectGroupAsync(members, options);
        await GroupWait.AssertConnectedAsync(conn);
        (conn.ActiveMember?.Name).Should().Be("beta");
        members.Should().AllSatisfy(member => member.IsUnhealthy.Should().BeFalse());
    }
}

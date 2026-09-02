using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class MultiPrimaryTests(ITestOutputHelper output) : TestBase(output)
{
    protected override string GetConfiguration() =>
        TestConfig.Current.PrimaryServerAndPort + "," + TestConfig.Current.SecureServerAndPort + ",password=" + TestConfig.Current.SecurePassword;

    [Fact]
    public async Task cannot_flush_replica()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        NoConcurrentRuntime();

        var ex = await Assert.ThrowsAsync<RedisCommandException>(async () =>
        {
            await using var conn = await ConnectionMultiplexer.ConnectAsync(TestConfig.Current.ReplicaServerAndPort + ",allowAdmin=true");

            var servers = conn.GetEndPoints().Select(e => conn.GetServer(e));
            var replica = servers.FirstOrDefault(x => x.IsReplica);
            Assert.NotNull(replica); // replica not found, ruh roh (and xunit's form narrows the null-state)
            replica.FlushDatabase();
        });
        ex.Message.Should().Be("Command cannot be issued to a replica: FLUSHDB");
    }

    [Fact]
    public void test_multi_no_tie_break()
    {
        var log = new StringBuilder();
        Writer.EchoTo(log);
        using (Create(log: Writer, tieBreaker: ""))
        {
            log.ToString().Should().Contain("Choosing primary arbitrarily");
        }
    }

    public static IEnumerable<object?[]> GetConnections()
    {
        yield return new object[] { TestConfig.Current.PrimaryServerAndPort, TestConfig.Current.PrimaryServerAndPort, TestConfig.Current.PrimaryServerAndPort };
        yield return new object[] { TestConfig.Current.SecureServerAndPort, TestConfig.Current.SecureServerAndPort, TestConfig.Current.SecureServerAndPort };
        yield return new object?[] { TestConfig.Current.SecureServerAndPort, TestConfig.Current.PrimaryServerAndPort, null };
        yield return new object?[] { TestConfig.Current.PrimaryServerAndPort, TestConfig.Current.SecureServerAndPort, null };

        yield return new object?[] { null, TestConfig.Current.PrimaryServerAndPort, null };
        yield return new object?[] { TestConfig.Current.PrimaryServerAndPort, null, TestConfig.Current.PrimaryServerAndPort };
        yield return new object?[] { null, TestConfig.Current.SecureServerAndPort, TestConfig.Current.SecureServerAndPort };
        yield return new object?[] { TestConfig.Current.SecureServerAndPort, null, TestConfig.Current.SecureServerAndPort };
        yield return new object?[] { null, null, null };
    }

    [Theory, MemberData(nameof(GetConnections))]
    public void test_multi_with_tiebreak(string a, string b, string elected)
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        const string TieBreak = "__tie__";
        // set the tie-breakers to the expected state
        using (var aConn = ConnectionMultiplexer.Connect(TestConfig.Current.PrimaryServerAndPort))
        {
            aConn.GetDatabase().StringSet(TieBreak, a);
        }
        using (var aConn = ConnectionMultiplexer.Connect(TestConfig.Current.SecureServerAndPort + ",password=" + TestConfig.Current.SecurePassword))
        {
            aConn.GetDatabase().StringSet(TieBreak, b);
        }

        // see what happens
        var log = new StringBuilder();
        Writer.EchoTo(log);

        using (Create(log: Writer, tieBreaker: TieBreak))
        {
            string text = log.ToString();
            text.Contains("failed to nominate").Should().BeFalse("failed to nominate");
            if (elected != null)
            {
                text.Contains("Elected: " + elected).Should().BeTrue("elected");
            }
            int nullCount = (a == null ? 1 : 0) + (b == null ? 1 : 0);
            if ((a == b && nullCount == 0) || nullCount == 1)
            {
                text.Contains("Election: Tie-breaker unanimous").Should().BeTrue("unanimous");
                text.Contains("Election: Choosing primary arbitrarily").Should().BeFalse("arbitrarily");
            }
            else
            {
                text.Contains("Election: Tie-breaker unanimous").Should().BeFalse("unanimous");
                text.Contains("Election: Choosing primary arbitrarily").Should().BeTrue("arbitrarily");
            }
        }
    }
}

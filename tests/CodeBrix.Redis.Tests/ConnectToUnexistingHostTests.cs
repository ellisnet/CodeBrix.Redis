using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ConnectToUnexistingHostTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task fails_within_timeout()
    {
        const int timeout = 1000;
        var sw = Stopwatch.StartNew();
        try
        {
            var config = new ConfigurationOptions
            {
                EndPoints = { { "invalid", 1234 } },
                ConnectTimeout = timeout,
            };

            await using (ConnectionMultiplexer.Connect(config, Writer))
            {
                await Task.Delay(10000, TestContext.Current.CancellationToken).ForAwait();
            }

            Assert.Fail("Connect should fail with RedisConnectionException exception");
        }
        catch (RedisConnectionException)
        {
            var elapsed = sw.ElapsedMilliseconds;
            Log("Elapsed time: " + elapsed);
            Log("Timeout: " + timeout);
            (elapsed < 9000).Should().BeTrue("Connect should fail within ConnectTimeout, ElapsedMs: " + elapsed);
        }
    }

    [Fact]
    public async Task can_not_open_nonsense_connection_ip()
    {
        await RunBlockingSynchronousWithExtraThreadAsync(InnerScenario).ForAwait();
        void InnerScenario()
        {
            var ex = Assert.Throws<RedisConnectionException>(() =>
            {
                using (ConnectionMultiplexer.Connect(TestConfig.Current.PrimaryServer + ":6500,connectTimeout=1000,connectRetry=0", Writer)) { }
            });
            Log(ex.ToString());
        }
    }

    [Fact]
    public async Task can_not_open_nonsense_connection_dns()
    {
        var ex = await Assert.ThrowsAsync<RedisConnectionException>(async () =>
        {
            using (await ConnectionMultiplexer.ConnectAsync($"doesnot.exist.ds.{Guid.NewGuid():N}.com:6500,connectTimeout=1000,connectRetry=0", Writer).ForAwait()) { }
        }).ForAwait();
        Log(ex.ToString());
    }

    [Fact]
    public async Task create_disconnected_nonsense_connection_ip()
    {
        await RunBlockingSynchronousWithExtraThreadAsync(InnerScenario).ForAwait();
        void InnerScenario()
        {
            using (var conn = ConnectionMultiplexer.Connect(TestConfig.Current.PrimaryServer + ":6500,abortConnect=false,connectTimeout=1000,connectRetry=0", Writer))
            {
                conn.GetServer(conn.GetEndPoints().Single()).IsConnected.Should().BeFalse();
                conn.GetDatabase().IsConnected(default(RedisKey)).Should().BeFalse();
            }
        }
    }

    [Fact]
    public async Task create_disconnected_nonsense_connection_dns()
    {
        await RunBlockingSynchronousWithExtraThreadAsync(InnerScenario).ForAwait();
        void InnerScenario()
        {
            using (var conn = ConnectionMultiplexer.Connect($"doesnot.exist.ds.{Guid.NewGuid():N}.com:6500,abortConnect=false,connectTimeout=1000,connectRetry=0", Writer))
            {
                conn.GetServer(conn.GetEndPoints().Single()).IsConnected.Should().BeFalse();
                conn.GetDatabase().IsConnected(default(RedisKey)).Should().BeFalse();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class SentinelTests(ITestOutputHelper output) : SentinelBase(output)
{
    [Fact]
    public async Task primary_connect_test()
    {
        SkipOnWindowsRelease();
        var connectionString = $"{TestConfig.Current.SentinelServer},serviceName={ServiceOptions.ServiceName},allowAdmin=true";

        var conn = ConnectionMultiplexer.Connect(connectionString);

        var db = conn.GetDatabase();
        await db.PingAsync();

        var endpoints = conn.GetEndPoints();
        endpoints.Length.Should().Be(2);

        var servers = endpoints.Select(e => conn.GetServer(e)).ToArray();
        servers.Length.Should().Be(2);

        var primary = servers.FirstOrDefault(s => !s.IsReplica);
        Assert.NotNull(primary);
        var replica = servers.FirstOrDefault(s => s.IsReplica);
        Assert.NotNull(replica);
        replica.EndPoint.ToString().Should().NotBe(primary.EndPoint.ToString());

        var expected = DateTime.Now.Ticks.ToString();
        Log("Tick Key: " + expected);
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, expected);

        var value = db.StringGet(key);
        value.Should().Be(expected);

        // force read from replica, replication has some lag
        await WaitForReplicationAsync(servers[0], TimeSpan.FromSeconds(10)).ForAwait();
        value = db.StringGet(key, CommandFlags.DemandReplica);
        value.Should().Be(expected);
    }

    [Fact]
    public async Task primary_connect_async_test()
    {
        SkipOnWindowsRelease();
        var connectionString = $"{TestConfig.Current.SentinelServer},serviceName={ServiceOptions.ServiceName},allowAdmin=true";
        var conn = await ConnectionMultiplexer.ConnectAsync(connectionString);

        var db = conn.GetDatabase();
        await db.PingAsync();

        var endpoints = conn.GetEndPoints();
        endpoints.Length.Should().Be(2);

        var servers = endpoints.Select(e => conn.GetServer(e)).ToArray();
        servers.Length.Should().Be(2);

        var primary = servers.FirstOrDefault(s => !s.IsReplica);
        Assert.NotNull(primary);
        var replica = servers.FirstOrDefault(s => s.IsReplica);
        Assert.NotNull(replica);
        replica.EndPoint.ToString().Should().NotBe(primary.EndPoint.ToString());

        var expected = DateTime.Now.Ticks.ToString();
        Log("Tick Key: " + expected);
        var key = Me();
        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);
        await db.StringSetAsync(key, expected);

        var value = await db.StringGetAsync(key);
        value.Should().Be(expected);

        // force read from replica, replication has some lag
        await WaitForReplicationAsync(servers[0], TimeSpan.FromSeconds(10)).ForAwait();
        value = await db.StringGetAsync(key, CommandFlags.DemandReplica);
        value.Should().Be(expected);
    }

    [Fact]
    [RunPerProtocol]
    public async Task sentinel_connect_test()
    {
        SkipOnWindowsRelease();
        var options = ServiceOptions.Clone();
        options.EndPoints.Add(TestConfig.Current.SentinelServer, TestConfig.Current.SentinelPortA);
        await using var conn = ConnectionMultiplexer.SentinelConnect(options);

        var db = conn.GetDatabase();
        var test = await db.PingAsync();
        Log("ping to sentinel {0}:{1} took {2} ms", TestConfig.Current.SentinelServer, TestConfig.Current.SentinelPortA, test.TotalMilliseconds);
    }

    [Fact]
    public async Task sentinel_repeat_connect_test()
    {
        SkipOnWindowsRelease();
        var options = ConfigurationOptions.Parse($"{TestConfig.Current.SentinelServer}:{TestConfig.Current.SentinelPortA}");
        options.ServiceName = ServiceName;
        options.AllowAdmin = true;

        Log("Service Name: " + options.ServiceName);
        foreach (var ep in options.EndPoints)
        {
            Log("  Endpoint: " + ep);
        }

        await using var conn = await ConnectionMultiplexer.ConnectAsync(options);

        var db = conn.GetDatabase();
        var test = await db.PingAsync();
        Log("ping to 1st sentinel {0}:{1} took {2} ms", TestConfig.Current.SentinelServer, TestConfig.Current.SentinelPortA, test.TotalMilliseconds);

        Log("Service Name: " + options.ServiceName);
        foreach (var ep in options.EndPoints)
        {
            Log("  Endpoint: " + ep);
        }

        await using var conn2 = ConnectionMultiplexer.Connect(options);

        var db2 = conn2.GetDatabase();
        var test2 = await db2.PingAsync();
        Log("ping to 2nd sentinel {0}:{1} took {2} ms", TestConfig.Current.SentinelServer, TestConfig.Current.SentinelPortA, test2.TotalMilliseconds);
    }

    [Fact]
    public async Task sentinel_connect_async_test()
    {
        SkipOnWindowsRelease();
        var options = ServiceOptions.Clone();
        options.EndPoints.Add(TestConfig.Current.SentinelServer, TestConfig.Current.SentinelPortA);
        var conn = await ConnectionMultiplexer.SentinelConnectAsync(options);

        var db = conn.GetDatabase();
        var test = await db.PingAsync();
        Log("ping to sentinel {0}:{1} took {2} ms", TestConfig.Current.SentinelServer, TestConfig.Current.SentinelPortA, test.TotalMilliseconds);
    }

    [Fact]
    public void sentinel_role()
    {
        SkipOnWindowsRelease();
        foreach (var server in SentinelsServers)
        {
            var role = server.Role();
            Assert.NotNull(role);
            RedisLiterals.sentinel.Should().Be(role.Value);
            var sentinel = role as Role.Sentinel;
            Assert.NotNull(sentinel);
        }
    }

    [Fact]
    public async Task ping_test()
    {
        SkipOnWindowsRelease();
        var test = await SentinelServerA.PingAsync();
        Log("ping to sentinel {0}:{1} took {2} ms", TestConfig.Current.SentinelServer, TestConfig.Current.SentinelPortA, test.TotalMilliseconds);
        test = await SentinelServerB.PingAsync();
        Log("ping to sentinel {0}:{1} took {1} ms", TestConfig.Current.SentinelServer, TestConfig.Current.SentinelPortB, test.TotalMilliseconds);
        test = await SentinelServerC.PingAsync();
        Log("ping to sentinel {0}:{1} took {1} ms", TestConfig.Current.SentinelServer, TestConfig.Current.SentinelPortC, test.TotalMilliseconds);
    }

    [Fact]
    public void sentinel_get_primary_address_by_name_test()
    {
        SkipOnWindowsRelease();
        foreach (var server in SentinelsServers)
        {
            var primary = server.SentinelMaster(ServiceName);
            var endpoint = server.SentinelGetMasterAddressByName(ServiceName);
            Assert.NotNull(endpoint);
            var ipEndPoint = endpoint as IPEndPoint;
            Assert.NotNull(ipEndPoint);
            ipEndPoint.Address.ToString().Should().Be(primary.ToDictionary()["ip"]);
            ipEndPoint.Port.ToString().Should().Be(primary.ToDictionary()["port"]);
            Log("{0}:{1}", ipEndPoint.Address, ipEndPoint.Port);
        }
    }

    [Fact]
    public async Task sentinel_get_primary_address_by_name_async_test()
    {
        SkipOnWindowsRelease();
        foreach (var server in SentinelsServers)
        {
            var primary = server.SentinelMaster(ServiceName);
            var endpoint = await server.SentinelGetMasterAddressByNameAsync(ServiceName).ForAwait();
            Assert.NotNull(endpoint);
            var ipEndPoint = endpoint as IPEndPoint;
            Assert.NotNull(ipEndPoint);
            ipEndPoint.Address.ToString().Should().Be(primary.ToDictionary()["ip"]);
            ipEndPoint.Port.ToString().Should().Be(primary.ToDictionary()["port"]);
            Log("{0}:{1}", ipEndPoint.Address, ipEndPoint.Port);
        }
    }

    [Fact]
    public void sentinel_get_master_address_by_name_negative_test()
    {
        SkipOnWindowsRelease();
        foreach (var server in SentinelsServers)
        {
            var endpoint = server.SentinelGetMasterAddressByName("FakeServiceName");
            endpoint.Should().BeNull();
        }
    }

    [Fact]
    public async Task sentinel_get_master_address_by_name_async_negative_test()
    {
        SkipOnWindowsRelease();
        foreach (var server in SentinelsServers)
        {
            var endpoint = await server.SentinelGetMasterAddressByNameAsync("FakeServiceName").ForAwait();
            endpoint.Should().BeNull();
        }
    }

    [Fact]
    public void sentinel_primary_test()
    {
        SkipOnWindowsRelease();
        foreach (var server in SentinelsServers)
        {
            var dict = server.SentinelMaster(ServiceName).ToDictionary();
            dict["name"].Should().Be(ServiceName);
            dict["flags"].Should().StartWith("master");
            foreach (var kvp in dict)
            {
                Log("{0}:{1}", kvp.Key, kvp.Value);
            }
        }
    }

    [Fact]
    public async Task sentinel_primary_async_test()
    {
        SkipOnWindowsRelease();
        foreach (var server in SentinelsServers)
        {
            var results = await server.SentinelMasterAsync(ServiceName).ForAwait();
            results.ToDictionary()["name"].Should().Be(ServiceName);
            results.ToDictionary()["flags"].Should().StartWith("master");
            foreach (var kvp in results)
            {
                Log("{0}:{1}", kvp.Key, kvp.Value);
            }
        }
    }

    [Fact]
    public void sentinel_sentinels_test()
    {
        SkipOnWindowsRelease();
        var sentinels = SentinelServerA.SentinelSentinels(ServiceName);

        var expected = new List<string?>
        {
            SentinelServerB.EndPoint.ToString(),
            SentinelServerC.EndPoint.ToString(),
        };

        var actual = new List<string>();
        foreach (var kv in sentinels)
        {
            var data = kv.ToDictionary();
            actual.Add(data["ip"] + ":" + data["port"]);
        }

        expected.Should().AllSatisfy(ep => ep.Should().NotBe(SentinelServerA.EndPoint.ToString()));
        sentinels.Length.Should().Be(2);
        expected.Should().AllSatisfy(ep => Assert.Contains(ep, actual, _ipComparer));

        sentinels = SentinelServerB.SentinelSentinels(ServiceName);
        foreach (var kv in sentinels)
        {
            var data = kv.ToDictionary();
            actual.Add(data["ip"] + ":" + data["port"]);
        }

        expected =
        [
            SentinelServerA.EndPoint.ToString(),
            SentinelServerC.EndPoint.ToString(),
        ];

        expected.Should().AllSatisfy(ep => ep.Should().NotBe(SentinelServerB.EndPoint.ToString()));
        sentinels.Length.Should().Be(2);
        expected.Should().AllSatisfy(ep => Assert.Contains(ep, actual, _ipComparer));

        sentinels = SentinelServerC.SentinelSentinels(ServiceName);
        foreach (var kv in sentinels)
        {
            var data = kv.ToDictionary();
            actual.Add(data["ip"] + ":" + data["port"]);
        }

        expected =
        [
            SentinelServerA.EndPoint.ToString(),
            SentinelServerB.EndPoint.ToString(),
        ];

        expected.Should().AllSatisfy(ep => ep.Should().NotBe(SentinelServerC.EndPoint.ToString()));
        sentinels.Length.Should().Be(2);
        expected.Should().AllSatisfy(ep => Assert.Contains(ep, actual, _ipComparer));
    }

    [Fact]
    public async Task sentinel_sentinels_async_test()
    {
        SkipOnWindowsRelease();
        var sentinels = await SentinelServerA.SentinelSentinelsAsync(ServiceName).ForAwait();
        var expected = new List<string?>
        {
            SentinelServerB.EndPoint.ToString(),
            SentinelServerC.EndPoint.ToString(),
        };

        var actual = new List<string>();
        foreach (var kv in sentinels)
        {
            var data = kv.ToDictionary();
            actual.Add(data["ip"] + ":" + data["port"]);
        }

        expected.Should().AllSatisfy(ep => ep.Should().NotBe(SentinelServerA.EndPoint.ToString()));
        sentinels.Length.Should().Be(2);
        expected.Should().AllSatisfy(ep => Assert.Contains(ep, actual, _ipComparer));

        sentinels = await SentinelServerB.SentinelSentinelsAsync(ServiceName).ForAwait();

        expected =
        [
            SentinelServerA.EndPoint.ToString(),
            SentinelServerC.EndPoint.ToString(),
        ];

        actual = [];
        foreach (var kv in sentinels)
        {
            var data = kv.ToDictionary();
            actual.Add(data["ip"] + ":" + data["port"]);
        }

        expected.Should().AllSatisfy(ep => ep.Should().NotBe(SentinelServerB.EndPoint.ToString()));
        sentinels.Length.Should().Be(2);
        expected.Should().AllSatisfy(ep => Assert.Contains(ep, actual, _ipComparer));

        sentinels = await SentinelServerC.SentinelSentinelsAsync(ServiceName).ForAwait();
        expected =
        [
            SentinelServerA.EndPoint.ToString(),
            SentinelServerB.EndPoint.ToString(),
        ];
        actual = [];
        foreach (var kv in sentinels)
        {
            var data = kv.ToDictionary();
            actual.Add(data["ip"] + ":" + data["port"]);
        }

        expected.Should().AllSatisfy(ep => ep.Should().NotBe(SentinelServerC.EndPoint.ToString()));
        sentinels.Length.Should().Be(2);
        expected.Should().AllSatisfy(ep => Assert.Contains(ep, actual, _ipComparer));
    }

    [Fact]
    public void sentinel_primaries_test()
    {
        SkipOnWindowsRelease();
        var primaryConfigs = SentinelServerA.SentinelMasters();
        primaryConfigs.Should().ContainSingle();
        primaryConfigs[0].ToDictionary().ContainsKey("name").Should().BeTrue("replicaConfigs contains 'name'");
        primaryConfigs[0].ToDictionary()["name"].Should().Be(ServiceName);
        primaryConfigs[0].ToDictionary()["flags"].Should().StartWith("master");
        foreach (var config in primaryConfigs)
        {
            foreach (var kvp in config)
            {
                Log("{0}:{1}", kvp.Key, kvp.Value);
            }
        }
    }

    [Fact]
    public async Task sentinel_primaries_async_test()
    {
        SkipOnWindowsRelease();
        var primaryConfigs = await SentinelServerA.SentinelMastersAsync().ForAwait();
        primaryConfigs.Should().ContainSingle();
        primaryConfigs[0].ToDictionary().ContainsKey("name").Should().BeTrue("replicaConfigs contains 'name'");
        primaryConfigs[0].ToDictionary()["name"].Should().Be(ServiceName);
        primaryConfigs[0].ToDictionary()["flags"].Should().StartWith("master");
        foreach (var config in primaryConfigs)
        {
            foreach (var kvp in config)
            {
                Log("{0}:{1}", kvp.Key, kvp.Value);
            }
        }
    }

    [Fact]
    public async Task sentinel_replicas_test()
    {
        SkipOnWindowsRelease();
        // Give previous test run a moment to reset when multi-framework failover is in play.
        await UntilConditionAsync(TimeSpan.FromSeconds(5), () => SentinelServerA.SentinelReplicas(ServiceName).Length > 0);

        var replicaConfigs = SentinelServerA.SentinelReplicas(ServiceName);
        (replicaConfigs.Length > 0).Should().BeTrue("Has replicaConfigs");
        replicaConfigs[0].ToDictionary().ContainsKey("name").Should().BeTrue("replicaConfigs contains 'name'");
        replicaConfigs[0].ToDictionary()["flags"].Should().StartWith("slave");

        foreach (var config in replicaConfigs)
        {
            foreach (var kvp in config)
            {
                Log("{0}:{1}", kvp.Key, kvp.Value);
            }
        }
    }

    [Fact]
    public async Task sentinel_replicas_async_test()
    {
        SkipOnWindowsRelease();
        // Give previous test run a moment to reset when multi-framework failover is in play.
        await UntilConditionAsync(TimeSpan.FromSeconds(5), () => SentinelServerA.SentinelReplicas(ServiceName).Length > 0);

        var replicaConfigs = await SentinelServerA.SentinelReplicasAsync(ServiceName).ForAwait();
        (replicaConfigs.Length > 0).Should().BeTrue("Has replicaConfigs");
        replicaConfigs[0].ToDictionary().ContainsKey("name").Should().BeTrue("replicaConfigs contains 'name'");
        replicaConfigs[0].ToDictionary()["flags"].Should().StartWith("slave");
        foreach (var config in replicaConfigs)
        {
            foreach (var kvp in config)
            {
                Log("{0}:{1}", kvp.Key, kvp.Value);
            }
        }
    }

    [Fact]
    public async Task sentinel_get_sentinel_addresses_test()
    {
        SkipOnWindowsRelease();
        var addresses = await SentinelServerA.SentinelGetSentinelAddressesAsync(ServiceName).ForAwait();
        addresses.Should().Contain(SentinelServerB.EndPoint);
        addresses.Should().Contain(SentinelServerC.EndPoint);

        addresses = await SentinelServerB.SentinelGetSentinelAddressesAsync(ServiceName).ForAwait();
        addresses.Should().Contain(SentinelServerA.EndPoint);
        addresses.Should().Contain(SentinelServerC.EndPoint);

        addresses = await SentinelServerC.SentinelGetSentinelAddressesAsync(ServiceName).ForAwait();
        addresses.Should().Contain(SentinelServerA.EndPoint);
        addresses.Should().Contain(SentinelServerB.EndPoint);
    }

    [Fact]
    public async Task read_only_connection_replicas_test()
    {
        SkipOnWindowsRelease();
        var replicas = SentinelServerA.SentinelGetReplicaAddresses(ServiceName);
        if (replicas.Length == 0)
        {
            Assert.Skip("Sentinel race: 0 replicas to test against.");
        }

        var config = new ConfigurationOptions();
        foreach (var replica in replicas)
        {
            config.EndPoints.Add(replica);
        }

        var readonlyConn = await ConnectionMultiplexer.ConnectAsync(config);

        await UntilConditionAsync(TimeSpan.FromSeconds(2), () => readonlyConn.IsConnected);
        readonlyConn.IsConnected.Should().BeTrue();
        var db = readonlyConn.GetDatabase();
        var s = db.StringGet(Me());
        s.IsNullOrEmpty.Should().BeTrue();
    }
}

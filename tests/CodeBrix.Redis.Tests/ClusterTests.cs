using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Redis.Profiling;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class ClusterTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    private const int NoRedirectRoutingProbeCount = 10;

    public enum StreamConsumerGroupRoutingOperation
    {
        SetPosition,
        ConsumerInfo,
        DeleteConsumer,
        DeleteConsumerGroup,
    }

    protected override string GetConfiguration() => GetClusterConfiguration();

    [Fact]
    public async Task export_configuration()
    {
        if (File.Exists("cluster.zip")) File.Delete("cluster.zip");
        File.Exists("cluster.zip").Should().BeFalse();
        await using (var conn = Create(allowAdmin: true))
        using (var file = File.Create("cluster.zip"))
        {
            conn.ExportConfiguration(file);
        }
        File.Exists("cluster.zip").Should().BeTrue();
    }

    [Fact]
    public async Task connect_uses_single_socket()
    {
        for (int i = 0; i < 5; i++)
        {
            await using var conn = Create(failMessage: i + ": ", log: Writer);

            foreach (var ep in conn.GetEndPoints())
            {
                var srv = conn.GetServer(ep);
                var counters = srv.GetCounters();
                Log($"{i}; interactive, {ep}, count: {counters.Interactive.SocketCount}");
                Log($"{i}; subscription, {ep}, count: {counters.Subscription.SocketCount}");
            }
            foreach (var ep in conn.GetEndPoints())
            {
                var srv = conn.GetServer(ep);
                var counters = srv.GetCounters();
                counters.Interactive.SocketCount.Should().Be(1);
                counters.Subscription.SocketCount.Should().Be(TestContext.Current.IsResp3() ? 0 : 1);
            }
        }
    }

    [Fact]
    public async Task can_get_total_stats()
    {
        await using var conn = Create();

        var counters = conn.GetCounters();
        Log(counters.ToString());
    }

    private void PrintEndpoints(EndPoint[] endpoints)
    {
        Log($"Endpoints Expected: {TestConfig.Current.ClusterStartPort}+{TestConfig.Current.ClusterServerCount}");
        Log("Endpoints Found:");
        foreach (var endpoint in endpoints)
        {
            Log("  Endpoint: " + endpoint);
        }
    }

    [Fact]
    public async Task connect()
    {
        await using var conn = Create(log: Writer);

        var expectedPorts = new HashSet<int>(Enumerable.Range(TestConfig.Current.ClusterStartPort, TestConfig.Current.ClusterServerCount));
        var endpoints = conn.GetEndPoints();
        if (TestConfig.Current.ClusterServerCount != endpoints.Length)
        {
            PrintEndpoints(endpoints);
        }

        endpoints.Length.Should().Be(TestConfig.Current.ClusterServerCount);
        int primaries = 0, replicas = 0;
        var failed = new List<EndPoint>();
        foreach (var endpoint in endpoints)
        {
            var server = conn.GetServer(endpoint);
            if (!server.IsConnected)
            {
                failed.Add(endpoint);
            }
            Log("endpoint:" + endpoint);
            server.EndPoint.Should().Be(endpoint);

            Log("endpoint-type:" + endpoint);
            endpoint.Should().BeOfType<IPEndPoint>();

            Log("port:" + endpoint);
            expectedPorts.Remove(((IPEndPoint)endpoint).Port).Should().BeTrue();

            Log("server-type:" + endpoint);
            server.ServerType.Should().Be(ServerType.Cluster);

            if (server.IsReplica) replicas++;
            else primaries++;
        }
        if (failed.Count != 0)
        {
            Log("{0} failues", failed.Count);
            foreach (var fail in failed)
            {
                Log(fail.ToString());
            }
            Assert.Fail("not all servers connected");
        }

        replicas.Should().Be(TestConfig.Current.ClusterServerCount / 2);
        primaries.Should().Be(TestConfig.Current.ClusterServerCount / 2);
    }

    [Fact]
    public async Task test_identity()
    {
        //Arrange
        await using var conn = Create();
        RedisKey key = Guid.NewGuid().ToByteArray();

        //Act
        var ep = conn.GetDatabase().IdentifyEndpoint(key);

        //Assert
        Assert.NotNull(ep);
        conn.GetServer(ep).ClusterConfiguration?.GetBySlot(key)?.EndPoint.Should().Be(ep);
    }

    [Fact]
    public async Task intentional_wrong_server()
    {
        SkipOnWindowsRelease();
        static string? StringGet(IServer server, RedisKey key, CommandFlags flags = CommandFlags.None)
            => (string?)server.Execute(0, "GET", [key], flags);

        await using var conn = Create();

        var endpoints = conn.GetEndPoints();
        var servers = endpoints.Select(e => conn.GetServer(e)).ToList();

        var key = Me();
        const string value = "abc";
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringSet(key, value, flags: CommandFlags.FireAndForget);
        await servers[0].PingAsync();
        var config = servers[0].ClusterConfiguration;
        Assert.NotNull(config);
        int slot = conn.HashSlot(key);
        var rightPrimaryNode = config.GetBySlot(key);
        Assert.NotNull(rightPrimaryNode);
        Log($"Right Primary: {rightPrimaryNode.EndPoint} {rightPrimaryNode.NodeId}");

        Assert.NotNull(rightPrimaryNode.EndPoint);
        string? a = StringGet(conn.GetServer(rightPrimaryNode.EndPoint), key);
        a.Should().Be(value); // right primary

        var node = config.Nodes.FirstOrDefault(x => !x.IsReplica && x.NodeId != rightPrimaryNode.NodeId);
        Assert.NotNull(node);
        Log($"Using Primary: {node.EndPoint} {node.NodeId}");
        {
            Assert.NotNull(node.EndPoint);
            string? b = StringGet(conn.GetServer(node.EndPoint), key);
            b.Should().Be(value); // wrong primary, allow redirect

            var ex = Assert.Throws<RedisServerException>(() => StringGet(conn.GetServer(node.EndPoint), key, CommandFlags.NoRedirect));
            ex.Message.Should().StartWith($"Key has MOVED to Endpoint {rightPrimaryNode.EndPoint} and hashslot {slot}");
        }

        node = config.Nodes.FirstOrDefault(x => x.IsReplica && x.ParentNodeId == rightPrimaryNode.NodeId);
        Assert.NotNull(node);
        {
            Assert.NotNull(node.EndPoint);
            string? d = StringGet(conn.GetServer(node.EndPoint), key);
            d.Should().Be(value); // right replica
        }

        node = config.Nodes.FirstOrDefault(x => x.IsReplica && x.ParentNodeId != rightPrimaryNode.NodeId);
        Assert.NotNull(node);
        {
            Assert.NotNull(node.EndPoint);
            string? e = StringGet(conn.GetServer(node.EndPoint), key);
            e.Should().Be(value); // wrong replica, allow redirect

            var ex = Assert.Throws<RedisServerException>(() => StringGet(conn.GetServer(node.EndPoint), key, CommandFlags.NoRedirect));
            ex.Message.Should().StartWith($"Key has MOVED to Endpoint {rightPrimaryNode.EndPoint} and hashslot {slot}");
        }
    }

    [Fact]
    public async Task cluster_no_redirect_routes_stream_create_consumer_group_by_key()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        for (var i = 0; i < NoRedirectRoutingProbeCount; i++)
        {
            var tag = Guid.NewGuid().ToString("N");
            RedisKey key = $"{{{tag}}}:stream:create-group";
            RedisValue group = $"group-{i}";
            Log("Probe {0}: key={1}, slot={2}", i, key, conn.HashSlot(key));

            await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

            (await db.StreamCreateConsumerGroupAsync(
                key,
                group,
                StreamPosition.NewMessages,
                createStream: true,
                flags: CommandFlags.NoRedirect)).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(StreamConsumerGroupRoutingOperation.SetPosition)]
    [InlineData(StreamConsumerGroupRoutingOperation.ConsumerInfo)]
    [InlineData(StreamConsumerGroupRoutingOperation.DeleteConsumer)]
    [InlineData(StreamConsumerGroupRoutingOperation.DeleteConsumerGroup)]
    public async Task cluster_no_redirect_routes_stream_consumer_group_metadata_by_key(StreamConsumerGroupRoutingOperation operation)
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        for (var i = 0; i < NoRedirectRoutingProbeCount; i++)
        {
            var tag = Guid.NewGuid().ToString("N");
            RedisKey key = $"{{{tag}}}:stream:consumer-group-metadata";
            RedisValue group = $"group-{i}";
            RedisValue consumer = $"consumer-{i}";
            Log("Probe {0}: key={1}, slot={2}", i, key, conn.HashSlot(key));

            await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);
            await db.StreamAddAsync(key, "field", "value", flags: CommandFlags.FireAndForget);
            await db.StreamCreateConsumerGroupAsync(key, group, StreamPosition.Beginning, flags: CommandFlags.FireAndForget);
            await db.StreamReadGroupAsync(key, group, consumer, StreamPosition.NewMessages, flags: CommandFlags.FireAndForget);

            switch (operation)
            {
                case StreamConsumerGroupRoutingOperation.SetPosition:
                    (await db.StreamConsumerGroupSetPositionAsync(key, group, StreamPosition.Beginning, CommandFlags.NoRedirect)).Should().BeTrue();
                    break;
                case StreamConsumerGroupRoutingOperation.ConsumerInfo:
                    var consumers = await db.StreamConsumerInfoAsync(key, group, CommandFlags.NoRedirect);
                    consumers.Should().Contain(consumerInfo => consumerInfo.Name == consumer);
                    break;
                case StreamConsumerGroupRoutingOperation.DeleteConsumer:
                    (await db.StreamDeleteConsumerAsync(key, group, consumer, CommandFlags.NoRedirect)).Should().Be(1);
                    break;
                case StreamConsumerGroupRoutingOperation.DeleteConsumerGroup:
                    (await db.StreamDeleteConsumerGroupAsync(key, group, CommandFlags.NoRedirect)).Should().BeTrue();
                    break;
            }
        }
    }

    [Fact]
    public async Task cluster_no_redirect_routes_set_intersection_length_by_keys()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        for (var i = 0; i < NoRedirectRoutingProbeCount; i++)
        {
            var tag = Guid.NewGuid().ToString("N");
            RedisKey key1 = $"{{{tag}}}:set:1";
            RedisKey key2 = $"{{{tag}}}:set:2";
            Log("Probe {0}: key={1}, slot={2}", i, key1, conn.HashSlot(key1));
            conn.HashSlot(key2).Should().Be(conn.HashSlot(key1));

            await db.KeyDeleteAsync([key1, key2], CommandFlags.FireAndForget);
            await db.SetAddAsync(key1, ["shared", "key1-only"], CommandFlags.FireAndForget);
            await db.SetAddAsync(key2, ["shared", "key2-only"], CommandFlags.FireAndForget);

            (await db.SetIntersectionLengthAsync([key1, key2], flags: CommandFlags.NoRedirect)).Should().Be(1);
        }
    }

    [Theory]
    [InlineData(SetOperation.Difference)]
    [InlineData(SetOperation.Intersect)]
    [InlineData(SetOperation.Union)]
    public async Task cluster_no_redirect_routes_sorted_set_combine_by_keys(SetOperation operation)
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        for (var i = 0; i < NoRedirectRoutingProbeCount; i++)
        {
            var tag = Guid.NewGuid().ToString("N");
            RedisKey key1 = $"{{{tag}}}:zset:1";
            RedisKey key2 = $"{{{tag}}}:zset:2";
            Log("Probe {0}: key={1}, slot={2}", i, key1, conn.HashSlot(key1));
            conn.HashSlot(key2).Should().Be(conn.HashSlot(key1));

            await db.KeyDeleteAsync([key1, key2], CommandFlags.FireAndForget);
            await db.SortedSetAddAsync(key1, [new("shared", 1), new("key1-only", 2)], CommandFlags.FireAndForget);
            await db.SortedSetAddAsync(key2, [new("shared", 1), new("key2-only", 3)], CommandFlags.FireAndForget);

            var result = await db.SortedSetCombineAsync(operation, [key1, key2], flags: CommandFlags.NoRedirect);
            switch (operation)
            {
                case SetOperation.Difference:
                    result.Should().Equal(["key1-only"]);
                    break;
                case SetOperation.Intersect:
                    result.Should().Equal(["shared"]);
                    break;
                case SetOperation.Union:
                    result.Length.Should().Be(3);
                    result.Should().Contain((RedisValue)"shared");
                    result.Should().Contain((RedisValue)"key1-only");
                    result.Should().Contain((RedisValue)"key2-only");
                    break;
            }
        }
    }

    [Fact]
    public async Task cluster_no_redirect_routes_sorted_set_intersection_length_by_keys()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        for (var i = 0; i < NoRedirectRoutingProbeCount; i++)
        {
            var tag = Guid.NewGuid().ToString("N");
            RedisKey key1 = $"{{{tag}}}:zset:1";
            RedisKey key2 = $"{{{tag}}}:zset:2";
            Log("Probe {0}: key={1}, slot={2}", i, key1, conn.HashSlot(key1));
            conn.HashSlot(key2).Should().Be(conn.HashSlot(key1));

            await db.KeyDeleteAsync([key1, key2], CommandFlags.FireAndForget);
            await db.SortedSetAddAsync(key1, [new("shared", 1), new("key1-only", 2)], CommandFlags.FireAndForget);
            await db.SortedSetAddAsync(key2, [new("shared", 1), new("key2-only", 3)], CommandFlags.FireAndForget);
            (await db.SortedSetIntersectionLengthAsync([key1, key2], flags: CommandFlags.NoRedirect)).Should().Be(1);
        }
    }

    [Fact]
    public async Task transaction_with_multi_server_keys()
    {
        await using var conn = Create();
        var ex = await Assert.ThrowsAsync<RedisCommandException>(async () =>
        {
            // connect
            var cluster = conn.GetDatabase();
            var anyServer = conn.GetServer(conn.GetEndPoints()[0]);
            await anyServer.PingAsync();
            anyServer.ServerType.Should().Be(ServerType.Cluster);
            var config = anyServer.ClusterConfiguration;
            Assert.NotNull(config);
            // invent 2 keys that we believe are served by different nodes
            string x = Guid.NewGuid().ToString(), y;
            var xNode = config.GetBySlot(x);
            Assert.NotNull(xNode);
            int abort = 1000;
            do
            {
                y = Guid.NewGuid().ToString();
            }
            while (--abort > 0 && config.GetBySlot(y) == xNode);
            if (abort == 0) Assert.Skip("failed to find a different node to use");
            var yNode = config.GetBySlot(y);
            Assert.NotNull(yNode);
            Log("x={0}, served by {1}", x, xNode.NodeId);
            Log("y={0}, served by {1}", y, yNode.NodeId);
            yNode.NodeId.Should().NotBe(xNode.NodeId);

            // wipe those keys
            cluster.KeyDelete(x, CommandFlags.FireAndForget);
            cluster.KeyDelete(y, CommandFlags.FireAndForget);

            // create a transaction that attempts to assign both keys
            var tran = cluster.CreateTransaction();
            tran.AddCondition(Condition.KeyNotExists(x));
            tran.AddCondition(Condition.KeyNotExists(y));
            _ = tran.StringSetAsync(x, "x-val");
            _ = tran.StringSetAsync(y, "y-val");
            tran.Execute();

            Assert.Fail("Expected single-slot rules to apply");
            // the rest no longer applies while we are following single-slot rules

            //// check that everything was aborted
            // success.Should().BeFalse("tran aborted");
            // setX.IsCanceled.Should().BeTrue("set x cancelled");
            // setY.IsCanceled.Should().BeTrue("set y cancelled");
            // var existsX = cluster.KeyExistsAsync(x);
            // var existsY = cluster.KeyExistsAsync(y);
            // cluster.Wait(existsX).Should().BeFalse("x exists");
            // cluster.Wait(existsY).Should().BeFalse("y exists");
        });
        ex.Message.Should().Be("Multi-key operations must involve a single slot; keys can use 'hash tags' to help this, i.e. '{/users/12345}/account' and '{/users/12345}/contacts' will always be in the same slot");
    }

    [Fact]
    public async Task transaction_with_same_server_keys()
    {
        await using var conn = Create();
        var ex = await Assert.ThrowsAsync<RedisCommandException>(async () =>
        {
            // connect
            var cluster = conn.GetDatabase();
            var anyServer = conn.GetServer(conn.GetEndPoints()[0]);
            await anyServer.PingAsync();
            var config = anyServer.ClusterConfiguration;
            Assert.NotNull(config);
            // invent 2 keys that we believe are served by different nodes
            string x = Guid.NewGuid().ToString(), y;
            var xNode = config.GetBySlot(x);
            int abort = 1000;
            do
            {
                y = Guid.NewGuid().ToString();
            }
            while (--abort > 0 && config.GetBySlot(y) != xNode);
            Assert.SkipWhen(abort == 0, "failed to find a key with the same node to use");
            var yNode = config.GetBySlot(y);
            Assert.NotNull(xNode);
            Log("x={0}, served by {1}", x, xNode.NodeId);
            Assert.NotNull(yNode);
            Log("y={0}, served by {1}", y, yNode.NodeId);
            yNode.NodeId.Should().Be(xNode.NodeId);

            // wipe those keys
            cluster.KeyDelete(x, CommandFlags.FireAndForget);
            cluster.KeyDelete(y, CommandFlags.FireAndForget);

            // create a transaction that attempts to assign both keys
            var tran = cluster.CreateTransaction();
            tran.AddCondition(Condition.KeyNotExists(x));
            tran.AddCondition(Condition.KeyNotExists(y));
            _ = tran.StringSetAsync(x, "x-val");
            _ = tran.StringSetAsync(y, "y-val");
            tran.Execute();

            Assert.Fail("Expected single-slot rules to apply");
            // the rest no longer applies while we are following single-slot rules

            //// check that everything was aborted
            // success.Should().BeTrue("tran aborted");
            // setX.IsCanceled.Should().BeFalse("set x cancelled");
            // setY.IsCanceled.Should().BeFalse("set y cancelled");
            // var existsX = cluster.KeyExistsAsync(x);
            // var existsY = cluster.KeyExistsAsync(y);
            // cluster.Wait(existsX).Should().BeTrue("x exists");
            // cluster.Wait(existsY).Should().BeTrue("y exists");
        });
        ex.Message.Should().Be("Multi-key operations must involve a single slot; keys can use 'hash tags' to help this, i.e. '{/users/12345}/account' and '{/users/12345}/contacts' will always be in the same slot");
    }

    [Fact]
    public async Task transaction_with_same_slot_keys()
    {
        await using var conn = Create();

        // connect
        var cluster = conn.GetDatabase();
        var anyServer = conn.GetServer(conn.GetEndPoints()[0]);
        await anyServer.PingAsync();
        var config = anyServer.ClusterConfiguration;
        Assert.NotNull(config);
        // invent 2 keys that we believe are in the same slot
        var guid = Guid.NewGuid().ToString();
        string x = "/{" + guid + "}/foo", y = "/{" + guid + "}/bar";

        conn.HashSlot(y).Should().Be(conn.HashSlot(x));
        var xNode = config.GetBySlot(x);
        var yNode = config.GetBySlot(y);
        Assert.NotNull(xNode);
        Log("x={0}, served by {1}", x, xNode.NodeId);
        Assert.NotNull(yNode);
        Log("y={0}, served by {1}", y, yNode.NodeId);
        yNode.NodeId.Should().Be(xNode.NodeId);

        // wipe those keys
        cluster.KeyDelete(x, CommandFlags.FireAndForget);
        cluster.KeyDelete(y, CommandFlags.FireAndForget);

        // create a transaction that attempts to assign both keys
        var tran = cluster.CreateTransaction();
        tran.AddCondition(Condition.KeyNotExists(x));
        tran.AddCondition(Condition.KeyNotExists(y));
        var setX = tran.StringSetAsync(x, "x-val");
        var setY = tran.StringSetAsync(y, "y-val");
        bool success = tran.Execute();

        // check that everything was aborted
        success.Should().BeTrue("tran aborted");
        setX.IsCanceled.Should().BeFalse("set x cancelled");
        setY.IsCanceled.Should().BeFalse("set y cancelled");
        var existsX = cluster.KeyExistsAsync(x);
        var existsY = cluster.KeyExistsAsync(y);
        cluster.Wait(existsX).Should().BeTrue("x exists");
        cluster.Wait(existsY).Should().BeTrue("y exists");
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(null, 100)]
    [InlineData("abc", 10)]
    [InlineData("abc", 100)]
    public async Task keys(string? pattern, int pageSize)
    {
        NoConcurrentRuntime();

        await using var conn = Create(allowAdmin: true);

        var dbId = TestConfig.GetDedicatedDB(conn);
        var server = conn.GetEndPoints().Select(x => conn.GetServer(x)).First(x => !x.IsReplica);
        await server.FlushDatabaseAsync(dbId);
        try
        {
            server.Keys(dbId, pattern: pattern, pageSize: pageSize).Any().Should().BeFalse();
            Log($"Complete: '{pattern}' / {pageSize}");
        }
        catch
        {
            Log($"Failed: '{pattern}' / {pageSize}");
            throw;
        }
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 7638)]
    [InlineData("{abc}", 7638)]
    [InlineData("abcdef", 15101)]
    [InlineData("abc{abc}def", 7638)]
    [InlineData("c", 7365)]
    [InlineData("g", 7233)]
    [InlineData("d", 11298)]

    [InlineData("user1000", 3443)]
    [InlineData("{user1000}", 3443)]
    [InlineData("abc{user1000}", 3443)]
    [InlineData("abc{user1000}def", 3443)]
    [InlineData("{user1000}.following", 3443)]
    [InlineData("{user1000}.followers", 3443)]

    [InlineData("foo{}{bar}", 8363)]

    [InlineData("foo{{bar}}zap", 4015)]
    [InlineData("{bar", 4015)]

    [InlineData("foo{bar}{zap}", 5061)]
    [InlineData("bar", 5061)]

    public async Task hash_slots(string key, int slot)
    {
        await using var conn = Create(connectTimeout: 5000);

        conn.HashSlot(key).Should().Be(slot);
    }

    [Fact]
    public async Task s_scan()
    {
        await using var conn = Create();

        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        int totalUnfiltered = 0, totalFiltered = 0;
        for (int i = 0; i < 1000; i++)
        {
            db.SetAdd(key, i, CommandFlags.FireAndForget);
            totalUnfiltered += i;
            if (i.ToString().Contains('3')) totalFiltered += i;
        }
        var unfilteredActual = db.SetScan(key).Select(x => (int)x).Sum();
        var filteredActual = db.SetScan(key, "*3*").Select(x => (int)x).Sum();
        unfilteredActual.Should().Be(totalUnfiltered);
        filteredActual.Should().Be(totalFiltered);
    }

    [Fact]
    public async Task get_config()
    {
        await using var conn = Create(allowAdmin: true, log: Writer);

        var endpoints = conn.GetEndPoints();
        var server = conn.GetServer(endpoints[0]);
        var nodes = server.ClusterNodes();
        Assert.NotNull(nodes);
        Log("Endpoints:");
        foreach (var endpoint in endpoints)
        {
            Log(endpoint.ToString());
        }
        Log("Nodes:");
        foreach (var node in nodes.Nodes.OrderBy(x => x))
        {
            Log(node.ToString());
        }

        endpoints.Length.Should().Be(TestConfig.Current.ClusterServerCount);
        nodes.Nodes.Count.Should().Be(TestConfig.Current.ClusterServerCount);
    }

    [Fact]
    public async Task access_random_keys()
    {
        Assert.Skip("FlushAllDatabases");

        NoConcurrentRuntime();

        await using var conn = Create(allowAdmin: true);

        var cluster = conn.GetDatabase();
        int slotMovedCount = 0;
        conn.HashSlotMoved += (s, a) =>
        {
            Assert.NotNull(a.OldEndPoint);
            Log("{0} moved from {1} to {2}", a.HashSlot, Describe(a.OldEndPoint), Describe(a.NewEndPoint));
            Interlocked.Increment(ref slotMovedCount);
        };
        var pairs = new Dictionary<string, string>();
        const int COUNT = 500;
        int index = 0;

        var servers = conn.GetEndPoints().Select(x => conn.GetServer(x)).ToList();
        foreach (var server in servers)
        {
            if (!server.IsReplica)
            {
                await server.PingAsync();
                await server.FlushAllDatabasesAsync();
            }
        }

        for (int i = 0; i < COUNT; i++)
        {
            var key = Guid.NewGuid().ToString();
            var value = Guid.NewGuid().ToString();
            pairs.Add(key, value);
            cluster.StringSet(key, value, flags: CommandFlags.FireAndForget);
        }

        var expected = new string[COUNT];
        var actual = new Task<RedisValue>[COUNT];
        index = 0;
        foreach (var pair in pairs)
        {
            expected[index] = pair.Value;
            actual[index] = cluster.StringGetAsync(pair.Key);
            index++;
        }
        cluster.WaitAll(actual);
        for (int i = 0; i < COUNT; i++)
        {
            actual[i].Result.Should().Be(expected[i]);
        }

        int total = 0;
        Parallel.ForEach(servers, server =>
        {
            if (!server.IsReplica)
            {
                int count = server.Keys(pageSize: 100).Count();
                Log("{0} has {1} keys", server.EndPoint, count);
                Interlocked.Add(ref total, count);
            }
        });

        foreach (var server in servers)
        {
            var counters = server.GetCounters();
            Log(counters.ToString());
        }
        int final = Volatile.Read(ref total);
        final.Should().Be(COUNT);
        Volatile.Read(ref slotMovedCount).Should().Be(0);
    }

    [Theory]
    [InlineData(CommandFlags.DemandMaster, false)]
    [InlineData(CommandFlags.DemandReplica, true)]
    [InlineData(CommandFlags.PreferMaster, false)]
    [InlineData(CommandFlags.PreferReplica, true)]
    public async Task get_from_right_node_based_on_flags(CommandFlags flags, bool isReplica)
    {
        await using var conn = Create(allowAdmin: true);

        var db = conn.GetDatabase();
        for (int i = 0; i < 500; i++)
        {
            var key = Guid.NewGuid().ToString();
            var endpoint = db.IdentifyEndpoint(key, flags);
            Assert.NotNull(endpoint);
            var server = conn.GetServer(endpoint);
            server.IsReplica.Should().Be(isReplica);
        }
    }

    private static string Describe(EndPoint endpoint) => endpoint?.ToString() ?? "(unknown)";

    [Fact]
    public async Task simple_profiling()
    {
        await using var conn = Create(log: Writer);

        var profiler = new ProfilingSession();
        var key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        conn.RegisterProfiler(() => profiler);
        db.StringSet(key, "world");
        var val = db.StringGet(key);
        val.Should().Be("world");

        var msgs = profiler.FinishProfiling().Where(m => m.Command == "GET" || m.Command == "SET").ToList();
        foreach (var msg in msgs)
        {
            Log("Profiler Message: " + Environment.NewLine + msg);
        }
        Log("Checking GET...");
        msgs.Should().Contain(m => m.Command == "GET");
        Log("Checking SET...");
        msgs.Should().Contain(m => m.Command == "SET");
        msgs.Count(m => m.RetransmissionOf is null).Should().Be(2);

        var arr = msgs.Where(m => m.RetransmissionOf is null).ToArray();
        arr[0].Command.Should().Be("SET");
        arr[1].Command.Should().Be("GET");
    }

    [Fact]
    public async Task multi_key_query_fails()
    {
        var keys = InventKeys(); // note the rules expected of this data are enforced in GroupedQueriesWork

        await using var conn = Create();

        var ex = Assert.Throws<RedisCommandException>(() => conn.GetDatabase(0).StringGet(keys));
        ex.Message.Should().Contain("Multi-key operations must involve a single slot");
    }

    private static RedisKey[] InventKeys()
    {
        RedisKey[] keys = new RedisKey[256];
        Random rand = new Random(12324);
        string InventString()
        {
            const string alphabet = "abcdefghijklmnopqrstuvwxyz012345689";
            var len = rand.Next(10, 50);
            char[] chars = new char[len];
            for (int i = 0; i < len; i++)
                chars[i] = alphabet[rand.Next(alphabet.Length)];
            return new string(chars);
        }

        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = InventString();
        }
        return keys;
    }

    [Fact]
    public async Task grouped_queries_work()
    {
        // note it doesn't matter that the data doesn't exist for this;
        // the point here is that the entire thing *won't work* otherwise,
        // as per above test
        var keys = InventKeys();
        await using var conn = Create();

        var grouped = keys.GroupBy(key => conn.GetHashSlot(key)).ToList();
        (grouped.Count > 1).Should().BeTrue(); // check not all a super-group
        (grouped.Count < keys.Length).Should().BeTrue(); // check not all singleton groups
        grouped.Sum(x => x.Count()).Should().Be(keys.Length); // check they're all there
        grouped.Should().Contain(x => x.Count() > 1); // check at least one group with multiple items (redundant from above, but... meh)

        Log($"{grouped.Count} groups, min: {grouped.Min(x => x.Count())}, max: {grouped.Max(x => x.Count())}, avg: {grouped.Average(x => x.Count())}");

        var db = conn.GetDatabase(0);
        var all = grouped.SelectMany(grp =>
        {
            var grpKeys = grp.ToArray();
            var values = db.StringGet(grpKeys);
            return grpKeys.Zip(values, (key, val) => new { key, val });
        }).ToDictionary(x => x.key, x => x.val);

        all.Count.Should().Be(keys.Length);
    }

    [Fact]
    public async Task moved_profiling()
    {
        var key = Me();
        const string Value = "redirected-value";

        var profiler = new ProfilingTests.PerThreadProfiler();

        await using var conn = Create();

        conn.RegisterProfiler(profiler.GetSession);

        var endpoints = conn.GetEndPoints();
        var servers = endpoints.Select(e => conn.GetServer(e));

        var db = conn.GetDatabase();
        db.KeyDelete(key);
        db.StringSet(key, Value);
        var config = servers.First().ClusterConfiguration;
        Assert.NotNull(config);
        // int slot = conn.HashSlot(Key);
        var rightPrimaryNode = config.GetBySlot(key);
        Assert.NotNull(rightPrimaryNode);
        Assert.NotNull(rightPrimaryNode.EndPoint);
        string? a = (string?)conn.GetServer(rightPrimaryNode.EndPoint).Execute(0, "GET", [key]);
        a.Should().Be(Value); // right primary

        var wrongPrimaryNode = config.Nodes.FirstOrDefault(x => !x.IsReplica && x.NodeId != rightPrimaryNode.NodeId);
        Assert.NotNull(wrongPrimaryNode);
        Assert.NotNull(wrongPrimaryNode.EndPoint);
        string? b = (string?)conn.GetServer(wrongPrimaryNode.EndPoint).Execute(0, "GET", [key]);
        b.Should().Be(Value); // wrong primary, allow redirect

        var msgs = profiler.GetSession().FinishProfiling().ToList();

        // verify that things actually got recorded properly, and the retransmission profilings are connected as expected
        {
            // expect 1 DEL, 1 SET, 1 GET (to right primary), 1 GET (to wrong primary) that was responded to by an ASK, and 1 GET (to right primary or a replica of it)
            msgs.Count.Should().Be(5);
            msgs.Count(c => c.Command == "DEL" || c.Command == "UNLINK").Should().Be(1);
            msgs.Count(c => c.Command == "SET").Should().Be(1);
            msgs.Count(c => c.Command == "GET").Should().Be(3);

            var toRightPrimaryNotRetransmission = msgs.Where(m => m.Command == "GET" && m.EndPoint.Equals(rightPrimaryNode.EndPoint) && m.RetransmissionOf == null);
            toRightPrimaryNotRetransmission.Should().ContainSingle();

            var toWrongPrimaryWithoutRetransmission = msgs.Where(m => m.Command == "GET" && m.EndPoint.Equals(wrongPrimaryNode.EndPoint) && m.RetransmissionOf == null).ToList();
            toWrongPrimaryWithoutRetransmission.Should().ContainSingle();

            var toRightPrimaryOrReplicaAsRetransmission = msgs.Where(m => m.Command == "GET" && (m.EndPoint.Equals(rightPrimaryNode.EndPoint) || rightPrimaryNode.Children.Any(c => m.EndPoint.Equals(c.EndPoint))) && m.RetransmissionOf != null).ToList();
            toRightPrimaryOrReplicaAsRetransmission.Should().ContainSingle();

            var originalWrongPrimary = toWrongPrimaryWithoutRetransmission.Single();
            var retransmissionToRight = toRightPrimaryOrReplicaAsRetransmission.Single();

            ReferenceEquals(originalWrongPrimary, retransmissionToRight.RetransmissionOf).Should().BeTrue();
        }

        foreach (var msg in msgs)
        {
            (msg.CommandCreated != default(DateTime)).Should().BeTrue();
            (msg.CreationToEnqueued > TimeSpan.Zero).Should().BeTrue();
            (msg.EnqueuedToSending > TimeSpan.Zero).Should().BeTrue();
            (msg.SentToResponse > TimeSpan.Zero).Should().BeTrue();
            (msg.ResponseToCompletion >= TimeSpan.Zero).Should().BeTrue(); // this can be immeasurably fast
            (msg.ElapsedTime > TimeSpan.Zero).Should().BeTrue();

            if (msg.RetransmissionOf != null)
            {
                // imprecision of DateTime.UtcNow makes this pretty approximate
                (msg.RetransmissionOf.CommandCreated <= msg.CommandCreated).Should().BeTrue();
                msg.RetransmissionReason.Should().Be(RetransmissionReasonType.Moved);
            }
            else
            {
                msg.RetransmissionReason.HasValue.Should().BeFalse();
            }
        }
    }

    [Fact]
    public async Task connect_includes_subscriber()
    {
        await using var conn = Create(keepAlive: 1, connectTimeout: 3000, shared: false);

        var db = conn.GetDatabase();
        await db.PingAsync();
        conn.IsConnected.Should().BeTrue();

        foreach (var server in conn.GetServerSnapshot())
        {
            server.InteractiveConnectionState.Should().Be(PhysicalBridge.State.ConnectedEstablished);
            server.SubscriptionConnectionState.Should().Be(PhysicalBridge.State.ConnectedEstablished);
        }
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    public async Task cluster_pub_sub(bool sharded, bool withKeyRouting, bool withKeyPrefix)
    {
        var guid = Guid.NewGuid().ToString();
        var channel = sharded ? RedisChannel.Sharded(guid) : RedisChannel.Literal(guid);
        if (withKeyRouting)
        {
            channel = channel.WithKeyRouting();
        }
        await using var conn = Create(
            keepAlive: 1,
            connectTimeout: 3000,
            shared: false,
            require: sharded ? RedisFeatures.v7_0_0_rc1 : RedisFeatures.v2_0_0,
            channelPrefix: withKeyPrefix ? "c_prefix:" : null);
        conn.IsConnected.Should().BeTrue();

        var pubsub = conn.GetSubscriber();
        HashSet<string> eps = [];
        for (int i = 0; i < 10; i++)
        {
            var ep = Format.ToString(await pubsub.IdentifyEndpointAsync(channel));
            Log($"Channel {channel} => {ep}");
            eps.Add(ep);
        }

        if (sharded | withKeyRouting)
        {
            eps.Should().ContainSingle();
        }
        else
        {
            // if not routed: we should have at least two different endpoints
            (eps.Count > 1).Should().BeTrue();
        }

        List<(RedisChannel, RedisValue)> received = [];
        var queue = await pubsub.SubscribeAsync(channel, CommandFlags.NoRedirect);
        _ = Task.Run(async () =>
        {
            // use queue API to have control over order
            await foreach (var item in queue)
            {
                lock (received)
                {
                    received.Add((item.Channel, item.Message));
                }
            }
        }, TestContext.Current.CancellationToken);
        var subscribedEp = Format.ToString(pubsub.SubscribedEndpoint(channel));
        Log($"Subscribed to {subscribedEp}");
        Assert.NotNull(subscribedEp);
        if (sharded | withKeyRouting)
        {
            subscribedEp.Should().Be(eps.Single());
        }
        var db = conn.GetDatabase();
        await Task.Delay(50, TestContext.Current.CancellationToken); // let the sub settle (this isn't needed on RESP3, note)
        await db.PingAsync();
        for (int i = 0; i < 10; i++)
        {
            // publish
            var receivers = await db.PublishAsync(channel, i.ToString());

            // check we get a hit (we are the only subscriber, and because we prefer to
            // use our own subscribed connection: we can reliably expect to see this hit)
            Log($"Published {i} to {receivers} receiver(s) against the receiving server.");
            receivers.Should().Be(1);
        }

        await Task.Delay(250, TestContext.Current.CancellationToken); // let the sub settle (this isn't needed on RESP3, note)
        await db.PingAsync();
        await pubsub.UnsubscribeAsync(channel);

        (RedisChannel Channel, RedisValue Value)[] snap;
        lock (received)
        {
            snap = received.ToArray(); // in case of concurrency
        }
        Log("items received: {0}", snap.Length);
        snap.Length.Should().Be(10);
        // separate log and validate loop here simplifies debugging (ask me how I know!)
        for (int i = 0; i < 10; i++)
        {
            var pair = snap[i];
            Log("element {0}: {1}/{2}", i, pair.Channel, pair.Value);
        }
        // even if not routed: we can expect the *order* to be correct, since there's
        // only one publisher (us), and we prefer to publish via our own subscription
        for (int i = 0; i < 10; i++)
        {
            var pair = snap[i];
            pair.Channel.Should().Be(channel);
            pair.Value.Should().Be(i);
        }
    }
}

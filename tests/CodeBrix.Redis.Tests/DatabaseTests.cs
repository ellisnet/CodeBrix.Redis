using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class DatabaseTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task command_count()
    {
        await using var conn = Create();
        var server = GetAnyPrimary(conn);
        var count = server.CommandCount();
        (count > 100).Should().BeTrue();

        count = await server.CommandCountAsync();
        (count > 100).Should().BeTrue();
    }

    [Fact]
    public async Task command_get_keys()
    {
        await using var conn = Create();
        var server = GetAnyPrimary(conn);

        RedisValue[] command = ["MSET", "a", "b", "c", "d", "e", "f"];

        RedisKey[] keys = server.CommandGetKeys(command);
        RedisKey[] expected = ["a", "c", "e"];
        expected.Should().Equal(keys);

        keys = await server.CommandGetKeysAsync(command);
        expected.Should().Equal(keys);
    }

    [Fact]
    public async Task command_list()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);
        var server = GetAnyPrimary(conn);

        var commands = server.CommandList();
        (commands.Length > 100).Should().BeTrue();
        commands = await server.CommandListAsync();
        (commands.Length > 100).Should().BeTrue();

        commands = server.CommandList(moduleName: "JSON");
        commands.Should().BeEmpty();
        commands = await server.CommandListAsync(moduleName: "JSON");
        commands.Should().BeEmpty();

        commands = server.CommandList(category: "admin");
        (commands.Length > 10).Should().BeTrue();
        commands = await server.CommandListAsync(category: "admin");
        (commands.Length > 10).Should().BeTrue();

        commands = server.CommandList(pattern: "a*");
        (commands.Length > 10).Should().BeTrue();
        commands = await server.CommandListAsync(pattern: "a*");
        (commands.Length > 10).Should().BeTrue();

        Assert.Throws<ArgumentException>(() => server.CommandList(moduleName: "JSON", pattern: "a*"));
        await Assert.ThrowsAsync<ArgumentException>(() => server.CommandListAsync(moduleName: "JSON", pattern: "a*"));
    }

    [Fact]
    public async Task count_keys()
    {
        NoConcurrentRuntime();

        var db1Id = TestConfig.GetDedicatedDB();
        var db2Id = TestConfig.GetDedicatedDB();
        await using (var conn = Create(allowAdmin: true))
        {
            Skip.IfMissingDatabase(conn, db1Id);
            Skip.IfMissingDatabase(conn, db2Id);
            var server = GetAnyPrimary(conn);
            server.FlushDatabase(db1Id, CommandFlags.FireAndForget);
            server.FlushDatabase(db2Id, CommandFlags.FireAndForget);
        }
        await using (var conn = Create(defaultDatabase: db2Id))
        {
            Skip.IfMissingDatabase(conn, db1Id);
            Skip.IfMissingDatabase(conn, db2Id);
            var key = Me();
            var dba = conn.GetDatabase(db1Id);
            var dbb = conn.GetDatabase(db2Id);
            dba.StringSet(key + ":abc", "def", flags: CommandFlags.FireAndForget);
            dba.StringIncrement(key, flags: CommandFlags.FireAndForget);
            dbb.StringIncrement(key, flags: CommandFlags.FireAndForget);

            var server = GetAnyPrimary(conn);
            var c0 = server.DatabaseSizeAsync(db1Id);
            var c1 = server.DatabaseSizeAsync(db2Id);
            var c2 = server.DatabaseSizeAsync(); // using default DB, which is db2Id

            (await c0).Should().Be(2);
            (await c1).Should().Be(1);
            (await c2).Should().Be(1);
        }
    }

    [Fact]
    public async Task database_count()
    {
        //Arrange
        await using var conn = Create(allowAdmin: true);
        var server = GetAnyPrimary(conn);
        var count = server.DatabaseCount;
        Log("Count: " + count);
        var configVal = server.ConfigGet("databases")[0].Value;

        //Act
        Log("Config databases: " + configVal);

        //Assert
        count.Should().Be(int.Parse(configVal));
    }

    [Fact]
    public async Task multi_databases()
    {
        await using var conn = Create();

        RedisKey key = Me();
        var db0 = conn.GetDatabase(TestConfig.GetDedicatedDB(conn));
        var db1 = conn.GetDatabase(TestConfig.GetDedicatedDB(conn));
        var db2 = conn.GetDatabase(TestConfig.GetDedicatedDB(conn));

        db0.KeyDelete(key, CommandFlags.FireAndForget);
        db1.KeyDelete(key, CommandFlags.FireAndForget);
        db2.KeyDelete(key, CommandFlags.FireAndForget);

        db0.StringSet(key, "a", flags: CommandFlags.FireAndForget);
        db1.StringSet(key, "b", flags: CommandFlags.FireAndForget);
        db2.StringSet(key, "c", flags: CommandFlags.FireAndForget);

        var a = db0.StringGetAsync(key);
        var b = db1.StringGetAsync(key);
        var c = db2.StringGetAsync(key);

        (await a).Should().Be("a"); // db:0
        (await b).Should().Be("b"); // db:1
        (await c).Should().Be("c"); // db:2
    }

    [Fact]
    public async Task swap_databases()
    {
        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v4_0_0);

        RedisKey key = Me();
        var db0id = TestConfig.GetDedicatedDB(conn);
        var db0 = conn.GetDatabase(db0id);
        var db1id = TestConfig.GetDedicatedDB(conn);
        var db1 = conn.GetDatabase(db1id);

        db0.KeyDelete(key, CommandFlags.FireAndForget);
        db1.KeyDelete(key, CommandFlags.FireAndForget);

        db0.StringSet(key, "a", flags: CommandFlags.FireAndForget);
        db1.StringSet(key, "b", flags: CommandFlags.FireAndForget);

        var a = db0.StringGetAsync(key);
        var b = db1.StringGetAsync(key);

        (await a).Should().Be("a"); // db:0
        (await b).Should().Be("b"); // db:1

        var server = GetServer(conn);
        server.SwapDatabases(db0id, db1id);

        var aNew = db1.StringGetAsync(key);
        var bNew = db0.StringGetAsync(key);

        (await aNew).Should().Be("a"); // db:1
        (await bNew).Should().Be("b"); // db:0
    }

    [Fact]
    public async Task swap_databases_async()
    {
        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v4_0_0);

        RedisKey key = Me();
        var db0id = TestConfig.GetDedicatedDB(conn);
        var db0 = conn.GetDatabase(db0id);
        var db1id = TestConfig.GetDedicatedDB(conn);
        var db1 = conn.GetDatabase(db1id);

        db0.KeyDelete(key, CommandFlags.FireAndForget);
        db1.KeyDelete(key, CommandFlags.FireAndForget);

        db0.StringSet(key, "a", flags: CommandFlags.FireAndForget);
        db1.StringSet(key, "b", flags: CommandFlags.FireAndForget);

        var a = db0.StringGetAsync(key);
        var b = db1.StringGetAsync(key);

        (await a).Should().Be("a"); // db:0
        (await b).Should().Be("b"); // db:1

        var server = GetServer(conn);
        _ = server.SwapDatabasesAsync(db0id, db1id).ForAwait();

        var aNew = db1.StringGetAsync(key);
        var bNew = db0.StringGetAsync(key);

        (await aNew).Should().Be("a"); // db:1
        (await bNew).Should().Be("b"); // db:0
    }
}

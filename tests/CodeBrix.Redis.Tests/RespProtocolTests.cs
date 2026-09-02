using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public sealed class RespProtocolTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    [RunPerProtocol]
    public async Task connect_with_timing()
    {
        await using var conn = Create(shared: false, log: Writer);
        await conn.GetDatabase().PingAsync();
    }

    [Theory]
    // specify nothing
    [InlineData("someserver", true)]
    // specify *just* the protocol; sure, we'll believe you
    [InlineData("someserver,protocol=resp3", true)]
    [InlineData("someserver,protocol=resp3,$HELLO=", false)]
    [InlineData("someserver,protocol=resp3,$HELLO=BONJOUR", true)]
    [InlineData("someserver,protocol=3", true, "resp3")]
    [InlineData("someserver,protocol=3,$HELLO=", false, "resp3")]
    [InlineData("someserver,protocol=3,$HELLO=BONJOUR", true, "resp3")]
    [InlineData("someserver,protocol=2", false, "resp2")]
    [InlineData("someserver,protocol=2,$HELLO=", false, "resp2")]
    [InlineData("someserver,protocol=2,$HELLO=BONJOUR", false, "resp2")]
    // specify a pre-6 version - only used if protocol specified
    [InlineData("someserver,version=5.9", false)]
    [InlineData("someserver,version=5.9,$HELLO=", false)]
    [InlineData("someserver,version=5.9,$HELLO=BONJOUR", false)]
    [InlineData("someserver,version=5.9,protocol=resp3", true)]
    [InlineData("someserver,version=5.9,protocol=resp3,$HELLO=", false)]
    [InlineData("someserver,version=5.9,protocol=resp3,$HELLO=BONJOUR", true)]
    [InlineData("someserver,version=5.9,protocol=3", true, "resp3")]
    [InlineData("someserver,version=5.9,protocol=3,$HELLO=", false, "resp3")]
    [InlineData("someserver,version=5.9,protocol=3,$HELLO=BONJOUR", true, "resp3")]
    [InlineData("someserver,version=5.9,protocol=2", false, "resp2")]
    [InlineData("someserver,version=5.9,protocol=2,$HELLO=", false, "resp2")]
    [InlineData("someserver,version=5.9,protocol=2,$HELLO=BONJOUR", false, "resp2")]
    // specify a post-6 version; attempt by default
    [InlineData("someserver,version=6.0", true)]
    [InlineData("someserver,version=6.0,$HELLO=", false)]
    [InlineData("someserver,version=6.0,$HELLO=BONJOUR", true)]
    [InlineData("someserver,version=6.0,protocol=resp3", true)]
    [InlineData("someserver,version=6.0,protocol=resp3,$HELLO=", false)]
    [InlineData("someserver,version=6.0,protocol=resp3,$HELLO=BONJOUR", true)]
    [InlineData("someserver,version=6.0,protocol=3", true, "resp3")]
    [InlineData("someserver,version=6.0,protocol=3,$HELLO=", false, "resp3")]
    [InlineData("someserver,version=6.0,protocol=3,$HELLO=BONJOUR", true, "resp3")]
    [InlineData("someserver,version=6.0,protocol=2", false, "resp2")]
    [InlineData("someserver,version=6.0,protocol=2,$HELLO=", false, "resp2")]
    [InlineData("someserver,version=6.0,protocol=2,$HELLO=BONJOUR", false, "resp2")]
    [InlineData("someserver,version=7.2", true)]
    [InlineData("someserver,version=7.2,$HELLO=", false)]
    [InlineData("someserver,version=7.2,$HELLO=BONJOUR", true)]
    public void parse_format_config_options(string configurationString, bool tryResp3, string? formatProtocol = null)
    {
        //Arrange
        var config = ConfigurationOptions.Parse(configurationString);

        //Act
        string expectedConfigurationString = formatProtocol is null ? configurationString : Regex.Replace(configurationString, "(?<=protocol=)[^,]+", formatProtocol);

        //Assert
        config.ToString(true).Should().Be(expectedConfigurationString);
        // check round-trip
        config.Clone().ToString(true).Should().Be(expectedConfigurationString);
        // check clone
        config.TryResp3().Should().Be(tryResp3);
    }

    [Fact]
    [RunPerProtocol]
    public async Task try_connect()
    {
        var muxer = Create(shared: false);
        await muxer.GetDatabase().PingAsync();

        var server = muxer.GetServerEndPoint(muxer.GetEndPoints().Single());
        if (TestContext.Current.IsResp3() && !server.GetFeatures().Resp3)
        {
            Assert.Skip("server does not support RESP3");
        }
        if (TestContext.Current.IsResp3())
        {
            server.Protocol.Should().Be(RedisProtocol.Resp3);
        }
        else
        {
            server.Protocol.Should().Be(RedisProtocol.Resp2);
        }
        var cid = server.GetBridge(RedisCommand.GET)?.ConnectionId;
        if (server.GetFeatures().ClientId)
        {
            Assert.NotNull(cid);
        }
        else
        {
            cid.Should().BeNull();
        }
    }

    [Theory]
    [InlineData("HELLO", true)]
    [InlineData("BONJOUR", false)]
    public async Task connect_with_broken_hello(string command, bool isResp3)
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        var config = ConfigurationOptions.Parse(TestConfig.Current.SecureServerAndPort);
        config.Password = TestConfig.Current.SecurePassword;
        config.Protocol = RedisProtocol.Resp3;
        config.CommandMap = CommandMap.Create(new() { ["hello"] = command });

        await using var muxer = await ConnectionMultiplexer.ConnectAsync(config, Writer);
        await muxer.GetDatabase().PingAsync(); // is connected
        var ep = muxer.GetServerEndPoint(muxer.GetEndPoints()[0]);
        if (!ep.GetFeatures().Resp3) // this is just a v6 check
        {
            isResp3 = false; // then, no: it won't be
        }
        ep.Protocol.Should().Be(isResp3 ? RedisProtocol.Resp3 : RedisProtocol.Resp2);
        var result = await muxer.GetDatabase().ExecuteAsync("latency", "doctor");
        result.Resp3Type.Should().Be(isResp3 ? ResultType.VerbatimString : ResultType.BulkString);
    }

    [Theory]
    [InlineData("return 42", RedisProtocol.Resp2, ResultType.Integer, ResultType.Integer, 42)]
    [InlineData("return 'abc'", RedisProtocol.Resp2, ResultType.BulkString, ResultType.BulkString, "abc")]
    [InlineData(@"return {1,2,3}", RedisProtocol.Resp2, ResultType.Array, ResultType.Array, ARR_123)]
    [InlineData("return nil", RedisProtocol.Resp2, ResultType.BulkString, ResultType.Null, null)]
    [InlineData(@"return redis.pcall('hgetall', '{key}')", RedisProtocol.Resp2, ResultType.Array, ResultType.Array, MAP_ABC)]
    [InlineData(@"redis.setresp(3) return redis.pcall('hgetall', '{key}')", RedisProtocol.Resp2, ResultType.Array, ResultType.Array, MAP_ABC)]
    [InlineData("return true", RedisProtocol.Resp2, ResultType.Integer, ResultType.Integer, 1)]
    [InlineData("return false", RedisProtocol.Resp2, ResultType.BulkString, ResultType.Null, null)]
    [InlineData("redis.setresp(3) return true", RedisProtocol.Resp2, ResultType.Integer, ResultType.Integer, 1)]
    [InlineData("redis.setresp(3) return false", RedisProtocol.Resp2, ResultType.Integer, ResultType.Integer, 0)]

    [InlineData("return { map = { a = 1, b = 2, c = 3 } }", RedisProtocol.Resp2, ResultType.Array, ResultType.Array, MAP_ABC, 6)]
    [InlineData("return { set = { a = 1, b = 2, c = 3 } }", RedisProtocol.Resp2, ResultType.Array, ResultType.Array, SET_ABC, 6)]
    [InlineData("return { double = 42 }", RedisProtocol.Resp2, ResultType.BulkString, ResultType.BulkString, 42.0, 6)]

    [InlineData("return 42", RedisProtocol.Resp3, ResultType.Integer, ResultType.Integer, 42)]
    [InlineData("return 'abc'", RedisProtocol.Resp3, ResultType.BulkString, ResultType.BulkString, "abc")]
    [InlineData("return {1,2,3}", RedisProtocol.Resp3, ResultType.Array, ResultType.Array, ARR_123)]
    [InlineData("return nil", RedisProtocol.Resp3, ResultType.BulkString, ResultType.Null, null)]
    [InlineData(@"return redis.pcall('hgetall', '{key}')", RedisProtocol.Resp3, ResultType.Array, ResultType.Array, MAP_ABC)]
    [InlineData(@"redis.setresp(3) return redis.pcall('hgetall', '{key}')", RedisProtocol.Resp3, ResultType.Array, ResultType.Map, MAP_ABC)]
    [InlineData("return true", RedisProtocol.Resp3, ResultType.Integer, ResultType.Integer, 1)]
    [InlineData("return false", RedisProtocol.Resp3, ResultType.BulkString, ResultType.Null, null)]
    [InlineData("redis.setresp(3) return true", RedisProtocol.Resp3, ResultType.Integer, ResultType.Boolean, true)]
    [InlineData("redis.setresp(3) return false", RedisProtocol.Resp3, ResultType.Integer, ResultType.Boolean, false)]

    [InlineData("return { map = { a = 1, b = 2, c = 3 } }", RedisProtocol.Resp3, ResultType.Array, ResultType.Map, MAP_ABC, 6)]
    [InlineData("return { set = { a = 1, b = 2, c = 3 } }", RedisProtocol.Resp3, ResultType.Array, ResultType.Set, SET_ABC, 6)]
    [InlineData("return { double = 42 }", RedisProtocol.Resp3, ResultType.SimpleString, ResultType.Double, 42.0, 6)]
    public async Task check_lua_result(string script, RedisProtocol protocol, ResultType resp2, ResultType resp3, object? expected, int? serverMin = 1)
    {
        // note Lua does not appear to return RESP3 types in any scenarios
        var muxer = Create(protocol: protocol);
        var ep = muxer.GetServerEndPoint(muxer.GetEndPoints().Single());
        if (serverMin > ep.Version.Major)
        {
            Assert.Skip($"applies to v{serverMin} onwards - detected v{ep.Version.Major}");
        }
        if (script.Contains("redis.setresp(3)") && !ep.GetFeatures().Resp3) /* v6 check */
        {
            Assert.Skip("debug protocol not available");
        }
        if (ep.Protocol is null) throw new InvalidOperationException($"No protocol! {ep.InteractiveConnectionState}");
        ep.Protocol.Should().Be(protocol);
        var key = Me();
        script = script.Replace("{key}", key);

        var db = muxer.GetDatabase();
        if (expected is MAP_ABC)
        {
            db.KeyDelete(key);
            db.HashSet(key, "a", 1);
            db.HashSet(key, "b", 2);
            db.HashSet(key, "c", 3);
        }
        var result = await db.ScriptEvaluateAsync(script: script, flags: CommandFlags.NoScriptCache);
        result.Resp2Type.Should().Be(resp2);
        result.Resp3Type.Should().Be(resp3);

        switch (expected)
        {
            case null:
                result.IsNull.Should().BeTrue();
                break;
            case ARR_123:
                result.Length.Should().Be(3);
                for (int i = 0; i < result.Length; i++)
                {
                    result[i].AsInt32().Should().Be(i + 1);
                }
                break;
            case MAP_ABC:
                var map = result.ToDictionary();
                map.Count.Should().Be(3);
                Assert.True(map.TryGetValue("a", out var value)); //kept as the xUnit form: it is annotated so the compiler's null-state flows to the use below
                value.AsInt32().Should().Be(1);
                Assert.True(map.TryGetValue("b", out value));
                value.AsInt32().Should().Be(2);
                Assert.True(map.TryGetValue("c", out value));
                value.AsInt32().Should().Be(3);
                break;
            case SET_ABC:
                result.Length.Should().Be(3);
                var arr = result.AsStringArray()!;
                arr.Should().Contain("a");
                arr.Should().Contain("b");
                arr.Should().Contain("c");
                break;
            case string s:
                result.AsString().Should().Be(s);
                break;
            case double d:
                result.AsDouble().Should().Be(d);
                break;
            case int i:
                result.AsInt32().Should().Be(i);
                break;
            case bool b:
                result.AsBoolean().Should().Be(b);
                break;
        }
    }

    [Theory]
    // [InlineData("return 42", false, ResultType.Integer, ResultType.Integer, 42)]
    // [InlineData("return 'abc'", false, ResultType.BulkString, ResultType.BulkString, "abc")]
    // [InlineData(@"return {1,2,3}", false, ResultType.Array, ResultType.Array, ARR_123)]
    // [InlineData("return nil", false, ResultType.BulkString, ResultType.Null, null)]
    // [InlineData(@"return redis.pcall('hgetall', 'key')", false, ResultType.Array, ResultType.Array, MAP_ABC)]
    // [InlineData("return true", false, ResultType.Integer, ResultType.Integer, 1)]

    // [InlineData("return 42", true, ResultType.Integer, ResultType.Integer, 42)]
    // [InlineData("return 'abc'", true, ResultType.BulkString, ResultType.BulkString, "abc")]
    // [InlineData("return {1,2,3}", true, ResultType.Array, ResultType.Array, ARR_123)]
    // [InlineData("return nil", true, ResultType.BulkString, ResultType.Null, null)]
    // [InlineData(@"return redis.pcall('hgetall', 'key')", true, ResultType.Array, ResultType.Array, MAP_ABC)]
    // [InlineData("return true", true, ResultType.Integer, ResultType.Integer, 1)]
    [InlineData("incrby", RedisProtocol.Resp2, ResultType.Integer, ResultType.Integer, 42, "ikey", 2)]
    [InlineData("incrby", RedisProtocol.Resp3, ResultType.Integer, ResultType.Integer, 42, "ikey", 2)]
    [InlineData("incrby", RedisProtocol.Resp2, ResultType.Integer, ResultType.Integer, 2, "nkey", 2)]
    [InlineData("incrby", RedisProtocol.Resp3, ResultType.Integer, ResultType.Integer, 2, "nkey", 2)]

    [InlineData("get", RedisProtocol.Resp2, ResultType.BulkString, ResultType.BulkString, "40", "ikey")]
    [InlineData("get", RedisProtocol.Resp3, ResultType.BulkString, ResultType.BulkString, "40", "ikey")]
    [InlineData("get", RedisProtocol.Resp2, ResultType.BulkString, ResultType.Null, null, "nkey")]
    [InlineData("get", RedisProtocol.Resp3, ResultType.BulkString, ResultType.Null, null, "nkey")]

    [InlineData("smembers", RedisProtocol.Resp2, ResultType.Array, ResultType.Array, SET_ABC, "skey")]
    [InlineData("smembers", RedisProtocol.Resp3, ResultType.Array, ResultType.Set, SET_ABC, "skey")]
    [InlineData("smembers", RedisProtocol.Resp2, ResultType.Array, ResultType.Array, EMPTY_ARR, "nkey")]
    [InlineData("smembers", RedisProtocol.Resp3, ResultType.Array, ResultType.Set, EMPTY_ARR, "nkey")]

    [InlineData("hgetall", RedisProtocol.Resp2, ResultType.Array, ResultType.Array, MAP_ABC, "hkey")]
    [InlineData("hgetall", RedisProtocol.Resp3, ResultType.Array, ResultType.Map, MAP_ABC, "hkey")]
    [InlineData("hgetall", RedisProtocol.Resp2, ResultType.Array, ResultType.Array, EMPTY_ARR, "nkey")]
    [InlineData("hgetall", RedisProtocol.Resp3, ResultType.Array, ResultType.Map, EMPTY_ARR, "nkey")]

    [InlineData("sismember", RedisProtocol.Resp2, ResultType.Integer, ResultType.Integer, true, "skey", "b")]
    [InlineData("sismember", RedisProtocol.Resp3, ResultType.Integer, ResultType.Integer, true, "skey", "b")]
    [InlineData("sismember", RedisProtocol.Resp2, ResultType.Integer, ResultType.Integer, false, "nkey", "b")]
    [InlineData("sismember", RedisProtocol.Resp3, ResultType.Integer, ResultType.Integer, false, "nkey", "b")]
    [InlineData("sismember", RedisProtocol.Resp2, ResultType.Integer, ResultType.Integer, false, "skey", "d")]
    [InlineData("sismember", RedisProtocol.Resp3, ResultType.Integer, ResultType.Integer, false, "skey", "d")]

    [InlineData("latency", RedisProtocol.Resp2, ResultType.BulkString, ResultType.BulkString, STR_DAVE, "doctor")]
    [InlineData("latency", RedisProtocol.Resp3, ResultType.BulkString, ResultType.VerbatimString, STR_DAVE, "doctor")]

    [InlineData("incrbyfloat", RedisProtocol.Resp2, ResultType.BulkString, ResultType.BulkString, 41.5, "ikey", 1.5)]
    [InlineData("incrbyfloat", RedisProtocol.Resp3, ResultType.BulkString, ResultType.BulkString, 41.5, "ikey", 1.5)]

    /* DEBUG PROTOCOL <type>
     * Reply with a test value of the specified type. <type> can be: string,
     * integer, double, bignum, null, array, set, map, attrib, push, verbatim,
     * true, false.,
     *
     * NOTE: "debug protocol" may be disabled in later default server configs; if this starts
     * failing when we upgrade the test server: update the config to re-enable the command
     */
    [InlineData("debug", RedisProtocol.Resp2, ResultType.BulkString, ResultType.BulkString, ANY, "protocol", "string")]
    [InlineData("debug", RedisProtocol.Resp3, ResultType.BulkString, ResultType.BulkString, ANY, "protocol", "string")]

    [InlineData("debug", RedisProtocol.Resp2, ResultType.BulkString, ResultType.BulkString, ANY, "protocol", "double")]
    [InlineData("debug", RedisProtocol.Resp3, ResultType.SimpleString, ResultType.Double, ANY, "protocol", "double")]

    [InlineData("debug", RedisProtocol.Resp2, ResultType.BulkString, ResultType.BulkString, ANY, "protocol", "bignum")]
    [InlineData("debug", RedisProtocol.Resp3, ResultType.SimpleString, ResultType.BigInteger, ANY, "protocol", "bignum")]

    [InlineData("debug", RedisProtocol.Resp2, ResultType.BulkString, ResultType.Null, null, "protocol", "null")]
    [InlineData("debug", RedisProtocol.Resp3, ResultType.BulkString, ResultType.Null, null, "protocol", "null")]

    [InlineData("debug", RedisProtocol.Resp2, ResultType.Array, ResultType.Array, ANY, "protocol", "array")]
    [InlineData("debug", RedisProtocol.Resp3, ResultType.Array, ResultType.Array, ANY, "protocol", "array")]

    [InlineData("debug", RedisProtocol.Resp2, ResultType.Array, ResultType.Array, ANY, "protocol", "set")]
    [InlineData("debug", RedisProtocol.Resp3, ResultType.Array, ResultType.Set, ANY, "protocol", "set")]

    [InlineData("debug", RedisProtocol.Resp2, ResultType.Array, ResultType.Array, ANY, "protocol", "map")]
    [InlineData("debug", RedisProtocol.Resp3, ResultType.Array, ResultType.Map, ANY, "protocol", "map")]

    [InlineData("debug", RedisProtocol.Resp2, ResultType.BulkString, ResultType.BulkString, ANY, "protocol", "verbatim")]
    [InlineData("debug", RedisProtocol.Resp3, ResultType.BulkString, ResultType.VerbatimString, ANY, "protocol", "verbatim")]

    [InlineData("debug", RedisProtocol.Resp2, ResultType.Integer, ResultType.Integer, true, "protocol", "true")]
    [InlineData("debug", RedisProtocol.Resp3, ResultType.Integer, ResultType.Boolean, true, "protocol", "true")]

    [InlineData("debug", RedisProtocol.Resp2, ResultType.Integer, ResultType.Integer, false, "protocol", "false")]
    [InlineData("debug", RedisProtocol.Resp3, ResultType.Integer, ResultType.Boolean, false, "protocol", "false")]

    public async Task check_command_result(string command, RedisProtocol protocol, ResultType resp2, ResultType resp3, object? expected, params object[] args)
    {
        var muxer = Create(protocol: protocol);
        var ep = muxer.GetServerEndPoint(muxer.GetEndPoints().Single());
        var usesDebugCommand = RedisCommandMetadata.TryParseCI(command, out var parsedCommand)
            && parsedCommand == RedisCommand.DEBUG;
        if (usesDebugCommand)
        {
            await AssertDebugCommandEnabledAsync(muxer);
        }
        if (usesDebugCommand && args.Length > 0 && args[0] is "protocol" && !ep.GetFeatures().Resp3 /* v6 check */)
        {
            Assert.Skip("debug protocol not available");
        }
        ep.Protocol.Should().Be(protocol);

        var db = muxer.GetDatabase();
        if (args.Length > 0)
        {
            var origKey = (string)args[0];
            switch (origKey)
            {
                case "ikey":
                case "skey":
                case "hkey":
                case "nkey":
                    var newKey = Me() + "_" + origKey; // disambiguate
                    args[0] = newKey;
                    await db.KeyDeleteAsync(newKey); // remove
                    switch (origKey) // initialize
                    {
                        case "ikey":
                            await db.StringSetAsync(newKey, "40");
                            break;
                        case "skey":
                            await db.SetAddAsync(newKey, ["a", "b", "c"]);
                            break;
                        case "hkey":
                            await db.HashSetAsync(newKey, [new("a", 1), new("b", 2), new("c", 3)]);
                            break;
                    }
                    break;
            }
        }
        var result = await db.ExecuteAsync(command, args);
        result.Resp2Type.Should().Be(resp2);
        result.Resp3Type.Should().Be(resp3);

        switch (expected)
        {
            case null:
                result.IsNull.Should().BeTrue();
                break;
            case ANY:
                // not checked beyond type
                break;
            case EMPTY_ARR:
                result.Length.Should().Be(0);
                break;
            case ARR_123:
                result.Length.Should().Be(3);
                for (int i = 0; i < result.Length; i++)
                {
                    result[i].AsInt32().Should().Be(i + 1);
                }
                break;
            case STR_DAVE:
                var scontent = result.ToString();
                Log(scontent);
                Assert.NotNull(scontent);
                var isExpectedContent = scontent.StartsWith("Dave, ") || scontent.StartsWith("I'm sorry, Dave");
                isExpectedContent.Should().BeTrue();
                Log(scontent);

                scontent = result.ToString(out var type);
                Assert.NotNull(scontent);
                isExpectedContent = scontent.StartsWith("Dave, ") || scontent.StartsWith("I'm sorry, Dave");
                isExpectedContent.Should().BeTrue();
                Log(scontent);
                if (protocol == RedisProtocol.Resp3)
                {
                    type.Should().Be("txt");
                }
                else
                {
                    type.Should().BeNull();
                }
                break;
            case SET_ABC:
                result.Length.Should().Be(3);
                var arr = result.AsStringArray()!;
                arr.Should().Contain("a");
                arr.Should().Contain("b");
                arr.Should().Contain("c");
                break;
            case MAP_ABC:
                var map = result.ToDictionary();
                map.Count.Should().Be(3);
                Assert.True(map.TryGetValue("a", out var value)); //kept as the xUnit form: it is annotated so the compiler's null-state flows to the use below
                value.AsInt32().Should().Be(1);
                Assert.True(map.TryGetValue("b", out value));
                value.AsInt32().Should().Be(2);
                Assert.True(map.TryGetValue("c", out value));
                value.AsInt32().Should().Be(3);
                break;
            case string s:
                result.AsString().Should().Be(s);
                break;
            case int i:
                result.AsInt32().Should().Be(i);
                break;
            case bool b:
                result.AsBoolean().Should().Be(b);
                result.AsInt32().Should().Be(b ? 1 : 0);
                result.AsInt64().Should().Be(b ? 1 : 0);
                break;
        }
    }

    private const string SET_ABC = nameof(SET_ABC);
    private const string ARR_123 = nameof(ARR_123);
    private const string MAP_ABC = nameof(MAP_ABC);
    private const string EMPTY_ARR = nameof(EMPTY_ARR);
    private const string STR_DAVE = nameof(STR_DAVE);
    private const string ANY = nameof(ANY);
}

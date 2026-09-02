using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CodeBrix.Redis.KeyspaceIsolation;
using SilverAssertions;
using Xunit;

// ReSharper disable UseAwaitUsing # for consistency with existing tests
// ReSharper disable MethodHasAsyncOverload # grandfathered existing usage
// ReSharper disable StringLiteralTypo # because of Lua scripts
namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class ScriptingTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    private IConnectionMultiplexer GetScriptConn(bool allowAdmin = false)
    {
        int syncTimeout = 5000;
        if (Debugger.IsAttached) syncTimeout = 500000;
        return Create(allowAdmin: allowAdmin, syncTimeout: syncTimeout, require: RedisFeatures.v2_6_0);
    }

    [Fact]
    public async Task client_scripting()
    {
        await using var conn = GetScriptConn();
        _ = conn.GetDatabase().ScriptEvaluate(script: "return redis.call('info','server')", keys: null, values: null);
    }

    [Fact]
    public async Task basic_scripting()
    {
        await using var conn = GetScriptConn();

        var db = conn.GetDatabase();
        var noCache = db.ScriptEvaluateAsync(
            script: "return {KEYS[1],KEYS[2],ARGV[1],ARGV[2]}",
            keys: ["key1", "key2"],
            values: ["first", "second"]);
        var cache = db.ScriptEvaluateAsync(
            script: "return {KEYS[1],KEYS[2],ARGV[1],ARGV[2]}",
            keys: ["key1", "key2"],
            values: ["first", "second"]);
        var results = (string[]?)(await noCache)!;
        Assert.NotNull(results);
        results.Length.Should().Be(4);
        results[0].Should().Be("key1");
        results[1].Should().Be("key2");
        results[2].Should().Be("first");
        results[3].Should().Be("second");

        results = (string[]?)(await cache)!;
        Assert.NotNull(results);
        results.Length.Should().Be(4);
        results[0].Should().Be("key1");
        results[1].Should().Be("key2");
        results[2].Should().Be("first");
        results[3].Should().Be("second");
    }

    [Fact]
    public async Task keys_scripting()
    {
        //Arrange
        await using var conn = GetScriptConn();

        var db = conn.GetDatabase();
        var key = Me();
        db.StringSet(key, "bar", flags: CommandFlags.FireAndForget);

        //Act
        var result = (string?)db.ScriptEvaluate(script: "return redis.call('get', KEYS[1])", keys: [key], values: null);

        //Assert
        result.Should().Be("bar");
    }

    [Fact]
    public async Task test_random_thing_from_forum()
    {
        //Arrange
        const string Script = """
                              local currentVal = tonumber(redis.call('GET', KEYS[1]));
                              if (currentVal <= 0 ) then return 1 elseif (currentVal - (tonumber(ARGV[1])) < 0 ) then return 0 end;
                              return redis.call('INCRBY', KEYS[1], -tonumber(ARGV[1]));
                              """;

        await using var conn = GetScriptConn();

        var prefix = Me();
        var db = conn.GetDatabase();
        db.StringSet(prefix + "A", "0", flags: CommandFlags.FireAndForget);
        db.StringSet(prefix + "B", "5", flags: CommandFlags.FireAndForget);
        db.StringSet(prefix + "C", "10", flags: CommandFlags.FireAndForget);

        var a = db.ScriptEvaluateAsync(script: Script, keys: [prefix + "A"], values: [6]).ForAwait();
        var b = db.ScriptEvaluateAsync(script: Script, keys: [prefix + "B"], values: [6]).ForAwait();
        var c = db.ScriptEvaluateAsync(script: Script, keys: [prefix + "C"], values: [6]).ForAwait();

        //Act
        var values = await db.StringGetAsync([prefix + "A", prefix + "B", prefix + "C"]).ForAwait();

        //Assert
        ((long)await a).Should().Be(1); // exit code when current val is non-positive
        ((long)await b).Should().Be(0); // exit code when result would be negative
        ((long)await c).Should().Be(4); // 10 - 6 = 4
        values[0].Should().Be("0");
        values[1].Should().Be("5");
        values[2].Should().Be("4");
    }

    [Fact]
    public async Task multi_incr_without_replies()
    {
        await using var conn = GetScriptConn();

        var db = conn.GetDatabase();
        var prefix = Me();
        // prime some initial values
        db.KeyDelete([prefix + "a", prefix + "b", prefix + "c"], CommandFlags.FireAndForget);
        db.StringIncrement(prefix + "b", flags: CommandFlags.FireAndForget);
        db.StringIncrement(prefix + "c", flags: CommandFlags.FireAndForget);
        db.StringIncrement(prefix + "c", flags: CommandFlags.FireAndForget);

        // run the script, passing "a", "b", "c", "c" to
        // increment a & b by 1, c twice
        var result = db.ScriptEvaluateAsync(
            script: "for i,key in ipairs(KEYS) do redis.call('incr', key) end",
            keys: [prefix + "a", prefix + "b", prefix + "c", prefix + "c"], // <== aka "KEYS" in the script
            values: null).ForAwait(); // <== aka "ARGV" in the script

        // check the incremented values
        var a = db.StringGetAsync(prefix + "a").ForAwait();
        var b = db.StringGetAsync(prefix + "b").ForAwait();
        var c = db.StringGetAsync(prefix + "c").ForAwait();

        var r = await result;
        Assert.NotNull(r);
        r.IsNull.Should().BeTrue("result");
        ((long)await a).Should().Be(1);
        ((long)await b).Should().Be(2);
        ((long)await c).Should().Be(4);
    }

    [Fact]
    public async Task multi_incr_by_without_replies()
    {
        await using var conn = GetScriptConn();

        var db = conn.GetDatabase();
        var prefix = Me();
        // prime some initial values
        db.KeyDelete([prefix + "a", prefix + "b", prefix + "c"], CommandFlags.FireAndForget);
        db.StringIncrement(prefix + "b", flags: CommandFlags.FireAndForget);
        db.StringIncrement(prefix + "c", flags: CommandFlags.FireAndForget);
        db.StringIncrement(prefix + "c", flags: CommandFlags.FireAndForget);

        // run the script, passing "a", "b", "c" and 1,2,3
        // increment a & b by 1, c twice
        var result = db.ScriptEvaluateAsync(
            script: "for i,key in ipairs(KEYS) do redis.call('incrby', key, ARGV[i]) end",
            keys: [prefix + "a", prefix + "b", prefix + "c"], // <== aka "KEYS" in the script
            values: [1, 1, 2]).ForAwait(); // <== aka "ARGV" in the script

        // check the incremented values
        var a = db.StringGetAsync(prefix + "a").ForAwait();
        var b = db.StringGetAsync(prefix + "b").ForAwait();
        var c = db.StringGetAsync(prefix + "c").ForAwait();

        ((await result).IsNull).Should().BeTrue("result");
        ((long)await a).Should().Be(1);
        ((long)await b).Should().Be(2);
        ((long)await c).Should().Be(4);
    }

    [Fact]
    public async Task disable_string_inference()
    {
        //Arrange
        await using var conn = GetScriptConn();

        var db = conn.GetDatabase();
        var key = Me();
        db.StringSet(key, "bar", flags: CommandFlags.FireAndForget);

        //Act
        var result = (byte[]?)db.ScriptEvaluate(script: "return redis.call('get', KEYS[1])", keys: [key]);

        //Assert
        Assert.NotNull(result);
        Encoding.UTF8.GetString(result).Should().Be("bar");
    }

    [Fact]
    public async Task flush_detection()
    {
        NoConcurrentRuntime();

        // we don't expect this to handle everything; we just expect it to be predictable
        await using var conn = GetScriptConn(allowAdmin: true);

        var db = conn.GetDatabase();
        var key = Me();
        db.StringSet(key, "bar", flags: CommandFlags.FireAndForget);
        var result = (string?)db.ScriptEvaluate(script: "return redis.call('get', KEYS[1])", keys: [key], values: null);
        result.Should().Be("bar");

        // now cause all kinds of problems
        GetServer(conn).ScriptFlush();

        // expect this one to <strike>fail</strike> just work fine (self-fix)
        db.ScriptEvaluate(script: "return redis.call('get', KEYS[1])", keys: [key], values: null);

        result = (string?)db.ScriptEvaluate(script: "return redis.call('get', KEYS[1])", keys: [key], values: null);
        result.Should().Be("bar");
    }

    [Fact]
    public async Task prepare_script()
    {
        NoConcurrentRuntime();

        string[] scripts = ["return redis.call('get', KEYS[1])", "return {KEYS[1],KEYS[2],ARGV[1],ARGV[2]}"];
        await using (var conn = GetScriptConn(allowAdmin: true))
        {
            var server = GetServer(conn);
            server.ScriptFlush();

            // when vanilla
            server.ScriptLoad(scripts[0]);
            server.ScriptLoad(scripts[1]);

            // when known to exist
            server.ScriptLoad(scripts[0]);
            server.ScriptLoad(scripts[1]);
        }
        await using (var conn = GetScriptConn())
        {
            var server = GetServer(conn);

            // when vanilla
            server.ScriptLoad(scripts[0]);
            server.ScriptLoad(scripts[1]);

            // when known to exist
            server.ScriptLoad(scripts[0]);
            server.ScriptLoad(scripts[1]);

            // when known to exist
            server.ScriptLoad(scripts[0]);
            server.ScriptLoad(scripts[1]);
        }
    }

    [Fact]
    public async Task non_ascii_scripts()
    {
        //Arrange
        await using var conn = GetScriptConn();

        const string Evil = "return '僕'";
        var db = conn.GetDatabase();
        GetServer(conn).ScriptLoad(Evil);

        //Act
        var result = (string?)db.ScriptEvaluate(script: Evil, keys: null, values: null);

        //Assert
        result.Should().Be("僕");
    }

    [Fact]
    public async Task script_throws_error()
    {
        await using var conn = GetScriptConn();
        await Assert.ThrowsAsync<RedisServerException>(async () =>
        {
            var db = conn.GetDatabase();
            try
            {
                await db.ScriptEvaluateAsync(script: "return redis.error_reply('oops')", keys: null, values: null).ForAwait();
            }
            catch (AggregateException ex)
            {
                throw ex.InnerExceptions[0];
            }
        }).ForAwait();
    }

    [Fact]
    public async Task script_throws_error_inside_transaction()
    {
        await using var conn = GetScriptConn();

        var key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var beforeTran = (string?)db.StringGet(key);
        beforeTran.Should().BeNull();
        var tran = db.CreateTransaction();
        {
            var a = tran.StringIncrementAsync(key);
            var b = tran.ScriptEvaluateAsync(script: "return redis.error_reply('oops')", keys: null, values: null);
            var c = tran.StringIncrementAsync(key);
            var complete = tran.ExecuteAsync();

            conn.Wait(complete).Should().BeTrue();
            QuickWait(a).IsCompleted.Should().BeTrue(a.Status.ToString());
            QuickWait(c).IsCompleted.Should().BeTrue("State: " + c.Status);
            a.Result.Should().Be(1L);
            c.Result.Should().Be(2L);

            QuickWait(b).IsFaulted.Should().BeTrue("should be faulted");
            Assert.NotNull(b.Exception);
            b.Exception.InnerExceptions.Should().ContainSingle();
            var ex = b.Exception.InnerExceptions.Single();
            ex.Should().BeOfType<RedisServerException>();
            // 7.0 slightly changes the error format, accept either.
            (new[] { "ERR oops", "oops" }).Should().Contain(ex.Message);
        }
        var afterTran = db.StringGetAsync(key);
        ((long)db.Wait(afterTran)).Should().Be(2L);
    }
    private static Task<T> QuickWait<T>(Task<T> task)
    {
        if (!task.IsCompleted)
        {
            try { task.Wait(200); } catch { /* But don't error */ }
        }
        return task;
    }

    [Fact]
    public async Task change_db_in_script()
    {
        await using var conn = GetScriptConn();

        var key = Me();
        conn.GetDatabase(1).StringSet(key, "db 1", flags: CommandFlags.FireAndForget);
        conn.GetDatabase(2).StringSet(key, "db 2", flags: CommandFlags.FireAndForget);

        Log("Key: " + key);
        var db = conn.GetDatabase(2);
        var evalResult = db.ScriptEvaluateAsync(
            script: @"redis.call('select', 1)
            return redis.call('get','" + key + "')",
            keys: null,
            values: null);
        var getResult = db.StringGetAsync(key);

        ((string?)await evalResult).Should().Be("db 1");
        // now, our connection thought it was in db 2, but the script changed to db 1
        (await getResult).Should().Be("db 2");
    }

    [Fact]
    public async Task change_db_in_tran_script()
    {
        await using var conn = GetScriptConn();

        var key = Me();
        conn.GetDatabase(1).StringSet(key, "db 1", flags: CommandFlags.FireAndForget);
        conn.GetDatabase(2).StringSet(key, "db 2", flags: CommandFlags.FireAndForget);

        var db = conn.GetDatabase(2);
        var tran = db.CreateTransaction();
        var evalResult = tran.ScriptEvaluateAsync(
            script: @"redis.call('select', 1)
            return redis.call('get','" + key + "')",
            keys: null,
            values: null);
        var getResult = tran.StringGetAsync(key);
        tran.Execute().Should().BeTrue();

        ((string?)await evalResult).Should().Be("db 1");
        // now, our connection thought it was in db 2, but the script changed to db 1
        (await getResult).Should().Be("db 2");
    }

    [Fact]
    public async Task test_basic_scripting()
    {
        await using var conn = Create(require: RedisFeatures.v2_6_0);

        RedisValue newId = Guid.NewGuid().ToString();
        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.HashSet(key, "id", 123, flags: CommandFlags.FireAndForget);

        var wasSet = (bool)db.ScriptEvaluate(
            script: "if redis.call('hexists', KEYS[1], 'UniqueId') then return redis.call('hset', KEYS[1], 'UniqueId', ARGV[1]) else return 0 end",
            keys: [key],
            values: [newId]);

        wasSet.Should().BeTrue();

        wasSet = (bool)db.ScriptEvaluate(
            script: "if redis.call('hexists', KEYS[1], 'UniqueId') then return redis.call('hset', KEYS[1], 'UniqueId', ARGV[1]) else return 0 end",
            keys: [key],
            values: [newId]);
        wasSet.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task check_loads(bool async)
    {
        NoConcurrentRuntime();

        await using var conn0 = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);
        await using var conn1 = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        // note that these are on different connections (so we wouldn't expect
        // the flush to drop the local cache - assume it is a surprise!)
        var server = conn0.GetServer(TestConfig.Current.PrimaryServerAndPort);
        var db = conn1.GetDatabase();
        var key = Me();
        var Script = $"return '{key}';";

        // start empty
        server.ScriptFlush();
        server.ScriptExists(Script).Should().BeFalse();

        // run once, causes to be cached
        (await EvaluateScript()).Should().Be(key);

        server.ScriptExists(Script).Should().BeTrue();

        // can run again
        (await EvaluateScript()).Should().Be(key);

        // ditch the scripts; should no longer exist
        await db.PingAsync();
        server.ScriptFlush();
        server.ScriptExists(Script).Should().BeFalse();
        await db.PingAsync();

        // just works; magic
        (await EvaluateScript()).Should().Be(key);

        // but gets marked as unloaded, so we can use it again...
        (await EvaluateScript()).Should().Be(key);

        // which will cause it to be cached
        server.ScriptExists(Script).Should().BeTrue();

        async Task<string?> EvaluateScript()
        {
            return async ?
            (string?)await db.ScriptEvaluateAsync(script: Script) :
            (string?)db.ScriptEvaluate(script: Script);
        }
    }

    [Fact]
    public async Task compare_script_to_direct()
    {
        NoConcurrentRuntime();

        Skip.UnlessLongRunning();
        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        const string Script = "return redis.call('incr', KEYS[1])";
        var server = conn.GetServer(TestConfig.Current.PrimaryServerAndPort);
        server.ScriptFlush();

        server.ScriptLoad(Script);
        var db = conn.GetDatabase();
        await db.PingAsync(); // k, we're all up to date now; clean db, minimal script cache

        // we're using a pipeline here, so send 1000 messages, but for timing: only care about the last
        const int Loop = 5000;
        RedisKey key = Me();
        RedisKey[] keys = [key]; // script takes an array

        // run via script
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var watch = Stopwatch.StartNew();
        for (int i = 1; i < Loop; i++) // the i=1 is to do all-but-one
        {
            db.ScriptEvaluate(script: Script, keys: keys, flags: CommandFlags.FireAndForget);
        }
        var scriptResult = db.ScriptEvaluate(script: Script, keys: keys); // last one we wait for (no F+F)
        watch.Stop();
        TimeSpan scriptTime = watch.Elapsed;

        // run via raw op
        db.KeyDelete(key, CommandFlags.FireAndForget);
        watch = Stopwatch.StartNew();
        for (int i = 1; i < Loop; i++) // the i=1 is to do all-but-one
        {
            db.StringIncrement(key, flags: CommandFlags.FireAndForget);
        }
        var directResult = db.StringIncrement(key); // last one we wait for (no F+F)
        watch.Stop();
        TimeSpan directTime = watch.Elapsed;

        ((long)scriptResult).Should().Be(Loop);
        directResult.Should().Be(Loop);

        Log("script: {0}ms; direct: {1}ms", scriptTime.TotalMilliseconds, directTime.TotalMilliseconds);
    }

    [Fact]
    public async Task test_call_by_hash()
    {
        NoConcurrentRuntime();

        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        const string Script = "return redis.call('incr', KEYS[1])";
        var server = conn.GetServer(TestConfig.Current.PrimaryServerAndPort);
        server.ScriptFlush();

        byte[] hash = server.ScriptLoad(Script);
        Assert.NotNull(hash);
        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        RedisKey[] keys = [key];

        string hexHash = string.Concat(hash.Select(x => x.ToString("X2")));
        hexHash.Should().Be("2BAB3B661081DB58BD2341920E0BA7CF5DC77B25");

        await db.ScriptEvaluateAsync(script: hexHash, keys: keys);
        await db.ScriptEvaluateAsync(hash, keys);

        var count = (int)db.StringGet(key);
        count.Should().Be(2);
    }

    [Fact]
    public async Task simple_lua_script()
    {
        NoConcurrentRuntime();

        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        const string Script = "return @ident";
        var server = conn.GetServer(TestConfig.Current.PrimaryServerAndPort);
        server.ScriptFlush();

        var prepared = LuaScript.Prepare(Script);

        var db = conn.GetDatabase();

        // Scopes for repeated use
        {
            var val = prepared.Evaluate(db, new { ident = "hello" });
            ((string?)val).Should().Be("hello");
        }

        {
            var val = prepared.Evaluate(db, new { ident = 123 });
            ((int)val).Should().Be(123);
        }

        {
            var val = prepared.Evaluate(db, new { ident = 123L });
            ((long)val).Should().Be(123L);
        }

        {
            var val = prepared.Evaluate(db, new { ident = 1.1 });
            ((double)val).Should().Be(1.1);
        }

        {
            var val = prepared.Evaluate(db, new { ident = true });
            ((bool)val).Should().BeTrue();
        }

        {
            var val = prepared.Evaluate(db, new { ident = new byte[] { 4, 5, 6 } });
            var valArray = (byte[]?)val;
            Assert.NotNull(valArray);
            (new byte[] { 4, 5, 6 }.SequenceEqual(valArray)).Should().BeTrue();
        }

        {
            var val = prepared.Evaluate(db, new { ident = new ReadOnlyMemory<byte>([4, 5, 6]) });
            var valArray = (byte[]?)val;
            Assert.NotNull(valArray);
            (new byte[] { 4, 5, 6 }.SequenceEqual(valArray)).Should().BeTrue();
        }
    }

    [Fact]
    public async Task simple_raw_script_evaluate()
    {
        NoConcurrentRuntime();

        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        const string Script = "return ARGV[1]";
        var server = conn.GetServer(TestConfig.Current.PrimaryServerAndPort);
        server.ScriptFlush();

        var db = conn.GetDatabase();

        // Scopes for repeated use
        {
            var val = db.ScriptEvaluate(script: Script, values: ["hello"]);
            ((string?)val).Should().Be("hello");
        }

        {
            var val = db.ScriptEvaluate(script: Script, values: [123]);
            ((int)val).Should().Be(123);
        }

        {
            var val = db.ScriptEvaluate(script: Script, values: [123L]);
            ((long)val).Should().Be(123L);
        }

        {
            var val = db.ScriptEvaluate(script: Script, values: [1.1]);
            ((double)val).Should().Be(1.1);
        }

        {
            var val = db.ScriptEvaluate(script: Script, values: [true]);
            ((bool)val).Should().BeTrue();
        }

        {
            var val = db.ScriptEvaluate(script: Script, values: [new byte[] { 4, 5, 6 }]);
            var valArray = (byte[]?)val;
            Assert.NotNull(valArray);
            (new byte[] { 4, 5, 6 }.SequenceEqual(valArray)).Should().BeTrue();
        }

        {
            var val = db.ScriptEvaluate(script: Script, values: [new ReadOnlyMemory<byte>([4, 5, 6])]);
            var valArray = (byte[]?)val;
            Assert.NotNull(valArray);
            (new byte[] { 4, 5, 6 }.SequenceEqual(valArray)).Should().BeTrue();
        }
    }

    [Fact]
    public async Task lua_script_with_keys()
    {
        NoConcurrentRuntime();

        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        const string Script = "redis.call('set', @key, @value)";
        var server = conn.GetServer(TestConfig.Current.PrimaryServerAndPort);
        server.ScriptFlush();

        var script = LuaScript.Prepare(Script);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var p = new { key = (RedisKey)key, value = 123 };

        script.Evaluate(db, p);
        var val = db.StringGet(key);
        ((int)val).Should().Be(123);

        // no super clean way to extract this; so just abuse InternalsVisibleTo
        script.ExtractParameters(p, null, out RedisKey[]? keys, out _);
        Assert.NotNull(keys);
        keys.Should().ContainSingle();
        keys[0].Should().Be(key);
    }

    [Fact]
    public async Task no_inline_replacement()
    {
        NoConcurrentRuntime();

        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        const string Script = "redis.call('set', @key, 'hello@example')";
        var server = conn.GetServer(TestConfig.Current.PrimaryServerAndPort);
        server.ScriptFlush();

        var script = LuaScript.Prepare(Script);

        script.ExecutableScript.Should().Be("redis.call('set', ARGV[1], 'hello@example')");

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var p = new { key };

        script.Evaluate(db, p, flags: CommandFlags.FireAndForget);
        var val = db.StringGet(key);
        val.Should().Be("hello@example");
    }

    [Fact]
    public void escape_replacement()
    {
        //Arrange
        const string Script = "redis.call('set', @key, @@escapeMe)";

        //Act
        var script = LuaScript.Prepare(Script);

        //Assert
        script.ExecutableScript.Should().Be("redis.call('set', ARGV[1], @escapeMe)");
    }

    [Fact]
    public async Task simple_loaded_lua_script()
    {
        NoConcurrentRuntime();

        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        const string Script = "return @ident";
        var server = conn.GetServer(TestConfig.Current.PrimaryServerAndPort);
        server.ScriptFlush();

        var prepared = LuaScript.Prepare(Script);
        var loaded = prepared.Load(server);

        var db = conn.GetDatabase();

        // Scopes for repeated use
        {
            var val = loaded.Evaluate(db, new { ident = "hello" });
            ((string?)val).Should().Be("hello");
        }

        {
            var val = loaded.Evaluate(db, new { ident = 123 });
            ((int)val).Should().Be(123);
        }

        {
            var val = loaded.Evaluate(db, new { ident = 123L });
            ((long)val).Should().Be(123L);
        }

        {
            var val = loaded.Evaluate(db, new { ident = 1.1 });
            ((double)val).Should().Be(1.1);
        }

        {
            var val = loaded.Evaluate(db, new { ident = true });
            ((bool)val).Should().BeTrue();
        }

        {
            var val = loaded.Evaluate(db, new { ident = new byte[] { 4, 5, 6 } });
            var valArray = (byte[]?)val;
            Assert.NotNull(valArray);
            (new byte[] { 4, 5, 6 }.SequenceEqual(valArray)).Should().BeTrue();
        }

        {
            var val = loaded.Evaluate(db, new { ident = new ReadOnlyMemory<byte>([4, 5, 6]) });
            var valArray = (byte[]?)val;
            Assert.NotNull(valArray);
            (new byte[] { 4, 5, 6 }.SequenceEqual(valArray)).Should().BeTrue();
        }
    }

    [Fact]
    public async Task loaded_lua_script_with_keys()
    {
        NoConcurrentRuntime();

        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        const string Script = "redis.call('set', @key, @value)";
        var server = conn.GetServer(TestConfig.Current.PrimaryServerAndPort);
        server.ScriptFlush();

        var script = LuaScript.Prepare(Script);
        var prepared = script.Load(server);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var p = new { key = (RedisKey)key, value = 123 };

        prepared.Evaluate(db, p, flags: CommandFlags.FireAndForget);
        var val = db.StringGet(key);
        ((int)val).Should().Be(123);

        // no super clean way to extract this; so just abuse InternalsVisibleTo
        prepared.Original.ExtractParameters(p, null, out RedisKey[]? keys, out _);
        Assert.NotNull(keys);
        keys.Should().ContainSingle();
        keys[0].Should().Be(key);
    }

    [Fact]
    public void purge_lua_script_cache()
    {
        const string Script = "redis.call('set', @PurgeLuaScriptCacheKey, @PurgeLuaScriptCacheValue)";
        var first = LuaScript.Prepare(Script);
        var fromCache = LuaScript.Prepare(Script);

        ReferenceEquals(first, fromCache).Should().BeTrue();

        LuaScript.PurgeCache();
        var shouldBeNew = LuaScript.Prepare(Script);

        ReferenceEquals(first, shouldBeNew).Should().BeFalse();
    }

    private static void PurgeLuaScriptOnFinalizeImpl(string script)
    {
        var first = LuaScript.Prepare(script);
        var fromCache = LuaScript.Prepare(script);
        ReferenceEquals(first, fromCache).Should().BeTrue();
        LuaScript.GetCachedScriptCount().Should().Be(1);
    }

    [Fact]
    public void purge_lua_script_on_finalize()
    {
        Skip.UnlessLongRunning();
        const string Script = "redis.call('set', @PurgeLuaScriptOnFinalizeKey, @PurgeLuaScriptOnFinalizeValue)";
        LuaScript.PurgeCache();
        LuaScript.GetCachedScriptCount().Should().Be(0);

        // This has to be a separate method to guarantee that the created LuaScript objects go out of scope,
        //   and are thus available to be garbage collected.
        PurgeLuaScriptOnFinalizeImpl(Script);
        CollectGarbage();

        LuaScript.GetCachedScriptCount().Should().Be(0);

        LuaScript.Prepare(Script);
        LuaScript.GetCachedScriptCount().Should().Be(1);
    }

    [Fact]
    public async Task database_lua_script_convenience_methods()
    {
        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        const string Script = "redis.call('set', @key, @value)";
        var script = LuaScript.Prepare(Script);
        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.ScriptEvaluate(script, new { key = (RedisKey)key, value = "value" });
        var val = db.StringGet(key);
        val.Should().Be("value");

        var prepared = script.Load(conn.GetServer(conn.GetEndPoints()[0]));

        db.ScriptEvaluate(prepared, new { key = (RedisKey)(key + "2"), value = "value2" });
        var val2 = db.StringGet(key + "2");
        val2.Should().Be("value2");
    }

    [Fact]
    public async Task server_lua_script_convenience_methods()
    {
        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        const string Script = "redis.call('set', @key, @value)";
        var script = LuaScript.Prepare(Script);
        var server = conn.GetServer(conn.GetEndPoints()[0]);
        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var prepared = server.ScriptLoad(script);

        db.ScriptEvaluate(prepared, new { key = (RedisKey)key, value = "value3" });
        var val = db.StringGet(key);
        val.Should().Be("value3");
    }

    [Fact]
    public void lua_script_prefixed_keys()
    {
        const string Script = "redis.call('set', @key, @value)";
        var prepared = LuaScript.Prepare(Script);
        var key = Me();
        var p = new { key = (RedisKey)key, value = "hello" };

        // no super clean way to extract this; so just abuse InternalsVisibleTo
        prepared.ExtractParameters(p, "prefix-", out RedisKey[]? keys, out RedisValue[]? args);
        Assert.NotNull(keys);
        keys.Should().ContainSingle();
        keys[0].Should().Be("prefix-" + key);
        Assert.NotNull(args);
        args.Length.Should().Be(2);
        args[0].Should().Be("prefix-" + key);
        args[1].Should().Be("hello");
    }

    [Fact]
    public async Task lua_script_with_wrapped_database()
    {
        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        const string Script = "redis.call('set', @key, @value)";
        var db = conn.GetDatabase();
        var wrappedDb = db.WithKeyPrefix("prefix-");
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var prepared = LuaScript.Prepare(Script);
        wrappedDb.ScriptEvaluate(prepared, new { key = (RedisKey)key, value = 123 });
        var val1 = wrappedDb.StringGet(key);
        ((int)val1).Should().Be(123);

        var val2 = db.StringGet("prefix-" + key);
        ((int)val2).Should().Be(123);

        var val3 = db.StringGet(key);
        val3.IsNull.Should().BeTrue();
    }

    [Fact]
    public async Task async_lua_script_with_wrapped_database()
    {
        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        const string Script = "redis.call('set', @key, @value)";
        var db = conn.GetDatabase();
        var wrappedDb = db.WithKeyPrefix("prefix-");
        var key = Me();
        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var prepared = LuaScript.Prepare(Script);
        await wrappedDb.ScriptEvaluateAsync(prepared, new { key = (RedisKey)key, value = 123 });
        var val1 = await wrappedDb.StringGetAsync(key);
        ((int)val1).Should().Be(123);

        var val2 = await db.StringGetAsync("prefix-" + key);
        ((int)val2).Should().Be(123);

        var val3 = await db.StringGetAsync(key);
        val3.IsNull.Should().BeTrue();
    }

    [Fact]
    public async Task loaded_lua_script_with_wrapped_database()
    {
        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        const string Script = "redis.call('set', @key, @value)";
        var db = conn.GetDatabase();
        var wrappedDb = db.WithKeyPrefix("prefix2-");
        var key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var server = conn.GetServer(conn.GetEndPoints()[0]);
        var prepared = LuaScript.Prepare(Script).Load(server);
        wrappedDb.ScriptEvaluate(prepared, new { key = (RedisKey)key, value = 123 }, flags: CommandFlags.FireAndForget);
        var val1 = wrappedDb.StringGet(key);
        ((int)val1).Should().Be(123);

        var val2 = db.StringGet("prefix2-" + key);
        ((int)val2).Should().Be(123);

        var val3 = db.StringGet(key);
        val3.IsNull.Should().BeTrue();
    }

    [Fact]
    public async Task async_loaded_lua_script_with_wrapped_database()
    {
        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_6_0);

        const string Script = "redis.call('set', @key, @value)";
        var db = conn.GetDatabase();
        var wrappedDb = db.WithKeyPrefix("prefix2-");
        var key = Me();
        await db.KeyDeleteAsync(key, CommandFlags.FireAndForget);

        var server = conn.GetServer(conn.GetEndPoints()[0]);
        var prepared = await LuaScript.Prepare(Script).LoadAsync(server);
        await wrappedDb.ScriptEvaluateAsync(prepared, new { key = (RedisKey)key, value = 123 }, flags: CommandFlags.FireAndForget);
        var val1 = await wrappedDb.StringGetAsync(key);
        ((int)val1).Should().Be(123);

        var val2 = await db.StringGetAsync("prefix2-" + key);
        ((int)val2).Should().Be(123);

        var val3 = await db.StringGetAsync(key);
        val3.IsNull.Should().BeTrue();
    }

    [Fact]
    public async Task script_with_key_prefix_via_tokens()
    {
        await using var conn = Create();

        var p = conn.GetDatabase().WithKeyPrefix("prefix/");

        var args = new { x = "abc", y = (RedisKey)"def", z = 123 };
        var script = LuaScript.Prepare(@"
local arr = {};
arr[1] = @x;
arr[2] = @y;
arr[3] = @z;
return arr;
");
        var result = (RedisValue[]?)p.ScriptEvaluate(script, args);
        Assert.NotNull(result);
        result[0].Should().Be("abc");
        result[1].Should().Be("prefix/def");
        result[2].Should().Be("123");
    }

    [Fact]
    public async Task script_with_key_prefix_via_arrays()
    {
        //Arrange
        await using var conn = Create();

        var p = conn.GetDatabase().WithKeyPrefix("prefix/");

        const string Script = @"
local arr = {};
arr[1] = ARGV[1];
arr[2] = KEYS[1];
arr[3] = ARGV[2];
return arr;
";

        //Act
        var result = (RedisValue[]?)p.ScriptEvaluate(script: Script, keys: ["def"], values: ["abc", 123]);

        //Assert
        Assert.NotNull(result);
        result[0].Should().Be("abc");
        result[1].Should().Be("prefix/def");
        result[2].Should().Be("123");
    }

    [Fact]
    public async Task script_with_key_prefix_compare()
    {
        await using var conn = Create();

        var p = conn.GetDatabase().WithKeyPrefix("prefix/");
        var args = new { k = (RedisKey)"key", s = "str", v = 123 };
        LuaScript lua = LuaScript.Prepare("return {@k, @s, @v}");
        var viaArgs = (RedisValue[]?)p.ScriptEvaluate(lua, args);

        var viaArr = (RedisValue[]?)p.ScriptEvaluate(script: "return {KEYS[1], ARGV[1], ARGV[2]}", keys: [args.k], values: [args.s, args.v]);
        Assert.NotNull(viaArr);
        Assert.NotNull(viaArgs);
        string.Join(",", viaArgs).Should().Be(string.Join(",", viaArr));
    }

    [Fact]
    public void redis_result_understands_null_array_array() => TestNullArray(RedisResult.NullArray);

    [Fact]
    public void redis_result_understands_null_array_null() => TestNullArray(null);

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("829c3804401b0727f70f73d4415e162400cbe57b", true)]
    [InlineData("$29c3804401b0727f70f73d4415e162400cbe57b", false)]
    [InlineData("829c3804401b0727f70f73d4415e162400cbe57", false)]
    [InlineData("829c3804401b0727f70f73d4415e162400cbe57bb", false)]
    public void sha1_detection(string? candidate, bool isSha) => ResultProcessor.ScriptLoadProcessor.IsSHA1(candidate).Should().Be(isSha);

    private static void TestNullArray(RedisResult? value)
    {
        (value == null || value.IsNull).Should().BeTrue();

        ((RedisValue[]?)value).Should().BeNull();
        ((RedisKey[]?)value).Should().BeNull();
        ((bool[]?)value).Should().BeNull();
        ((long[]?)value).Should().BeNull();
        ((ulong[]?)value).Should().BeNull();
        ((string[]?)value!).Should().BeNull();
        ((int[]?)value).Should().BeNull();
        ((double[]?)value).Should().BeNull();
        ((byte[][]?)value!).Should().BeNull();
        ((RedisResult[]?)value).Should().BeNull();
    }

    [Fact]
    public void redis_result_understands_null_null() => TestNullValue(null);
    [Fact]
    public void redis_result_understands_null_value() => TestNullValue(RedisResult.Create(RedisValue.Null, ResultType.None));

    [Fact]
    public async Task test_eval_readonly()
    {
        //Arrange
        await using var conn = GetScriptConn();
        var db = conn.GetDatabase();

        string script = "return KEYS[1]";
        RedisKey key = Me();
        RedisKey[] keys = [key];
        RedisValue[] values = ["first"];

        //Act
        var result = db.ScriptEvaluateReadOnly(script, keys, values);

        //Assert
        result.ToString().Should().Be(key.ToString());
    }

    [Fact]
    public async Task test_eval_readonly_async()
    {
        //Arrange
        await using var conn = GetScriptConn();
        var db = conn.GetDatabase();

        string script = "return KEYS[1]";
        RedisKey key = Me();
        RedisKey[] keys = [key];
        RedisValue[] values = ["first"];

        //Act
        var result = await db.ScriptEvaluateReadOnlyAsync(script, keys, values);

        //Assert
        result.ToString().Should().Be(key.ToString());
    }

    [Fact]
    public async Task test_eval_sha_read_only()
    {
        //Arrange
        await using var conn = GetScriptConn();
        var db = conn.GetDatabase();
        var key = Me();
        var script = $"return redis.call('get','{key}')";
        db.StringSet(key, "bar");
        db.ScriptEvaluate(script: script);

        SHA1 sha1Hash = SHA1.Create();
        byte[] hash = sha1Hash.ComputeHash(Encoding.UTF8.GetBytes(script));
        Log("Hash: " + Convert.ToBase64String(hash));

        //Act
        var result = db.ScriptEvaluateReadOnly(hash);

        //Assert
        result.ToString().Should().Be("bar");
    }

    [Fact]
    public async Task test_eval_sha_read_only_async()
    {
        //Arrange
        await using var conn = GetScriptConn();
        var db = conn.GetDatabase();
        var key = Me();
        var script = $"return redis.call('get','{key}')";
        db.StringSet(key, "bar");
        db.ScriptEvaluate(script: script);

        SHA1 sha1Hash = SHA1.Create();
        byte[] hash = sha1Hash.ComputeHash(Encoding.UTF8.GetBytes(script));
        Log("Hash: " + Convert.ToBase64String(hash));

        //Act
        var result = await db.ScriptEvaluateReadOnlyAsync(hash);

        //Assert
        result.ToString().Should().Be("bar");
    }

    [Fact, TestCulture("en-US")]
    public void lua_script_english_parameters() => LuaScriptParameterShared();

    [Fact, TestCulture("tr-TR")]
    public void lua_script_turkish_parameters() => LuaScriptParameterShared();

    private void LuaScriptParameterShared()
    {
        const string Script = "redis.call('set', @key, @testIId)";
        var prepared = LuaScript.Prepare(Script);
        var key = Me();
        var p = new { key = (RedisKey)key, testIId = "hello" };

        prepared.ExtractParameters(p, null, out RedisKey[]? keys, out RedisValue[]? args);
        Assert.NotNull(keys);
        keys.Should().ContainSingle();
        keys[0].Should().Be(key);
        Assert.NotNull(args);
        args.Length.Should().Be(2);
        args[0].Should().Be(key);
        args[1].Should().Be("hello");
    }

    private static void TestNullValue(RedisResult? value)
    {
        (value == null || value.IsNull).Should().BeTrue();

        (((RedisValue)value).IsNull).Should().BeTrue();
        (((RedisKey)value).IsNull).Should().BeTrue();
        ((bool?)value).Should().BeNull();
        ((long?)value).Should().BeNull();
        ((ulong?)value).Should().BeNull();
        ((string?)value).Should().BeNull();
        ((double?)value).Should().BeNull();
        ((byte[]?)value).Should().BeNull();
    }
}

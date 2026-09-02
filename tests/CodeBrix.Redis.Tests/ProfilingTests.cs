using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Redis.Profiling;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(NonParallelCollection.Name)]
public class ProfilingTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task simple()
    {
        await using var conn = Create();

        var server = conn.GetServer(TestConfig.Current.PrimaryServerAndPort);
        var script = LuaScript.Prepare("return redis.call('get', @key)");
        var loaded = script.Load(server);
        var key = Me();

        var session = new ProfilingSession();

        conn.RegisterProfiler(() => session);

        var dbId = TestConfig.GetDedicatedDB(conn);
        var db = conn.GetDatabase(dbId);
        db.StringSet(key, "world");
        var result = db.ScriptEvaluate(script, new { key = (RedisKey)key });
        Assert.NotNull(result);
        result.AsString().Should().Be("world");
        var loadedResult = db.ScriptEvaluate(loaded, new { key = (RedisKey)key });
        Assert.NotNull(loadedResult);
        loadedResult.AsString().Should().Be("world");
        var val = db.StringGet(key);
        val.Should().Be("world");
        var s = (string?)db.Execute("ECHO", "fii");
        s.Should().Be("fii");

        var cmds = session.FinishProfiling();
        var evalCmds = cmds.Where(c => c.Command == "EVAL").ToList();
        evalCmds.Count.Should().Be(2);
        var i = 0;
        foreach (var cmd in cmds)
        {
            Log($"Command {i++} (DB: {cmd.Db}): {cmd?.ToString()?.Replace("\n", ", ")}");
        }

        var all = string.Join(",", cmds.Select(x => x.Command));
        all.Should().Be("SET,EVAL,EVAL,GET,ECHO");
        Log("Checking for SET");
        var set = cmds.SingleOrDefault(cmd => cmd.Command == "SET");
        Assert.NotNull(set);
        Log("Checking for GET");
        var get = cmds.SingleOrDefault(cmd => cmd.Command == "GET");
        Assert.NotNull(get);
        Log("Checking for EVAL");
        var eval1 = evalCmds[0];
        Log("Checking for EVAL");
        var eval2 = evalCmds[1];
        var echo = cmds.SingleOrDefault(cmd => cmd.Command == "ECHO");
        Assert.NotNull(echo);
        cmds.Count().Should().Be(5);

        (set.CommandCreated <= eval1.CommandCreated).Should().BeTrue();
        (eval1.CommandCreated <= eval2.CommandCreated).Should().BeTrue();
        (eval2.CommandCreated <= get.CommandCreated).Should().BeTrue();

        AssertProfiledCommandValues(set, conn, dbId);

        AssertProfiledCommandValues(get, conn, dbId);

        AssertProfiledCommandValues(eval1, conn, dbId);

        AssertProfiledCommandValues(eval2, conn, dbId);

        AssertProfiledCommandValues(echo, conn, -1); // we recognize ECHO as db-free
    }

    private static void AssertProfiledCommandValues(IProfiledCommand command, IConnectionMultiplexer conn, int dbId)
    {
        command.Db.Should().Be(dbId);
        command.EndPoint.Should().Be(conn.GetEndPoints()[0]);
        (command.CreationToEnqueued > TimeSpan.Zero).Should().BeTrue(nameof(command.CreationToEnqueued));
        (command.EnqueuedToSending > TimeSpan.Zero).Should().BeTrue(nameof(command.EnqueuedToSending));
        (command.SentToResponse > TimeSpan.Zero).Should().BeTrue(nameof(command.SentToResponse));
        (command.ResponseToCompletion >= TimeSpan.Zero).Should().BeTrue(nameof(command.ResponseToCompletion));
        (command.ElapsedTime > TimeSpan.Zero).Should().BeTrue(nameof(command.ElapsedTime));
        (command.ElapsedTime > command.CreationToEnqueued && command.ElapsedTime > command.EnqueuedToSending && command.ElapsedTime > command.SentToResponse).Should().BeTrue("Comparisons");
        (command.RetransmissionOf == null).Should().BeTrue(nameof(command.RetransmissionOf));
        (command.RetransmissionReason == null).Should().BeTrue(nameof(command.RetransmissionReason));
    }

    [Fact]
    public async Task many_threads()
    {
        Skip.UnlessLongRunning();
        await using var conn = Create();

        var session = new ProfilingSession();
        var prefix = Me();

        conn.RegisterProfiler(() => session);

        var threads = new List<Thread>();
        const int CountPer = 100;
        for (var i = 1; i <= 16; i++)
        {
            var db = conn.GetDatabase(i);

            threads.Add(new Thread(() =>
            {
                var threadTasks = new List<Task>();

                for (var j = 0; j < CountPer; j++)
                {
                    var task = db.StringSetAsync(prefix + j, "" + j);
                    threadTasks.Add(task);
                }

                Task.WaitAll(threadTasks.ToArray());
            }));
        }

        threads.ForEach(thread => thread.Start());
        threads.ForEach(thread => thread.Join());

        var allVals = session.FinishProfiling();
        var relevant = allVals.Where(cmd => cmd.Db > 0).ToList();

        var kinds = relevant.Select(cmd => cmd.Command).Distinct().ToList();
        foreach (var k in kinds)
        {
            Log("Kind Seen: " + k);
        }
        (kinds.Count <= 2).Should().BeTrue();
        kinds.Should().Contain("SET");
        if (kinds.Count == 2 && !kinds.Contains("SELECT") && !kinds.Contains("GET"))
        {
            Assert.Fail("Non-SET, Non-SELECT, Non-GET command seen");
        }

        relevant.Count.Should().Be(16 * CountPer);
        relevant.Select(cmd => cmd.Db).Distinct().Count().Should().Be(16);

        for (var i = 1; i <= 16; i++)
        {
            var setsInDb = relevant.Count(cmd => cmd.Db == i);
            setsInDb.Should().Be(CountPer);
        }
    }

    [Fact]
    public async Task many_contexts()
    {
        Skip.UnlessLongRunning();
        await using var conn = Create();

        var profiler = new AsyncLocalProfiler();
        var prefix = Me();
        conn.RegisterProfiler(profiler.GetSession);

        var tasks = new Task[16];

        var results = new ProfiledCommandEnumerable[tasks.Length];

        for (var i = 0; i < tasks.Length; i++)
        {
            var ix = i;
            tasks[ix] = Task.Run(async () =>
            {
                var db = conn.GetDatabase(ix);

                var allTasks = new List<Task>();

                for (var j = 0; j < 1000; j++)
                {
                    var g = db.StringGetAsync(prefix + ix);
                    var s = db.StringSetAsync(prefix + ix, "world" + ix);
                    // overlap the g+s, just for fun
                    await g;
                    await s;
                }

                results[ix] = profiler.GetSession().FinishProfiling();
            }, TestContext.Current.CancellationToken);
        }
        Task.WhenAll(tasks).Wait();

        for (var i = 0; i < results.Length; i++)
        {
            var res = results[i];

            var numGets = res.Count(r => r.Command == "GET");
            var numSets = res.Count(r => r.Command == "SET");

            numGets.Should().Be(1000);
            numSets.Should().Be(1000);
            res.All(cmd => cmd.Db == i).Should().BeTrue();
        }
    }

    internal sealed class PerThreadProfiler
    {
        private readonly ThreadLocal<ProfilingSession> perThreadSession = new ThreadLocal<ProfilingSession>(() => new ProfilingSession());

        public ProfilingSession GetSession() => perThreadSession.Value!;
    }

    internal sealed class AsyncLocalProfiler
    {
        private readonly AsyncLocal<ProfilingSession> perThreadSession = new AsyncLocal<ProfilingSession>();

        public ProfilingSession GetSession()
        {
            var val = perThreadSession.Value;
            if (val == null)
            {
                perThreadSession.Value = val = new ProfilingSession();
            }
            return val;
        }
    }

    [Fact]
    public async Task low_allocation_enumerable()
    {
        await using var conn = Create();

        const int OuterLoop = 1000;
        var session = new ProfilingSession();
        conn.RegisterProfiler(() => session);

        var prefix = Me();
        var db = conn.GetDatabase(1);

        var allTasks = new List<Task<string?>>();

        foreach (var i in Enumerable.Range(0, OuterLoop))
        {
            var t = db.StringSetAsync(prefix + i, "bar" + i).ContinueWith(async _ => (string?)(await db.StringGetAsync(prefix + i).ForAwait()));

            var finalResult = t.Unwrap();
            allTasks.Add(finalResult);
        }

        conn.WaitAll(allTasks.ToArray());

        var res = session.FinishProfiling();
        res.GetType().IsValueType.Should().BeTrue();

        using (var e = res.GetEnumerator())
        {
            e.GetType().IsValueType.Should().BeTrue();

            e.MoveNext().Should().BeTrue();
            var i = e.Current;

            e.Reset();
            e.MoveNext().Should().BeTrue();
            var j = e.Current;

            ReferenceEquals(i, j).Should().BeTrue();
        }

        res.Count(r => r.Command == "GET" && r.Db > 0).Should().Be(OuterLoop);
        res.Count(r => r.Command == "SET" && r.Db > 0).Should().Be(OuterLoop);
        res.Count(r => r.Db > 0).Should().Be(OuterLoop * 2);
    }

    [Fact]
    public async Task profiling_md_ex1()
    {
        Skip.UnlessLongRunning();
        await using var conn = Create();

        var session = new ProfilingSession();
        var prefix = Me();

        conn.RegisterProfiler(() => session);

        var threads = new List<Thread>();

        for (var i = 0; i < 16; i++)
        {
            var db = conn.GetDatabase(i);

            var thread = new Thread(() =>
            {
                var threadTasks = new List<Task>();

                for (var j = 0; j < 1000; j++)
                {
                    var task = db.StringSetAsync(prefix + j, "" + j);
                    threadTasks.Add(task);
                }

                Task.WaitAll(threadTasks.ToArray());
            });

            threads.Add(thread);
        }

        threads.ForEach(thread => thread.Start());
        threads.ForEach(thread => thread.Join());

        IEnumerable<IProfiledCommand> timings = session.FinishProfiling();

        timings.Count().Should().Be(16000);
    }

    [Fact]
    public async Task profiling_md_ex2()
    {
        Skip.UnlessLongRunning();
        await using var conn = Create();

        var profiler = new PerThreadProfiler();
        var prefix = Me();

        conn.RegisterProfiler(profiler.GetSession);

        var threads = new List<Thread>();

        var perThreadTimings = new ConcurrentDictionary<Thread, List<IProfiledCommand>>();

        for (var i = 0; i < 16; i++)
        {
            var db = conn.GetDatabase(i);

            var thread = new Thread(() =>
            {
                var threadTasks = new List<Task>();

                for (var j = 0; j < 1000; j++)
                {
                    var task = db.StringSetAsync(prefix + j, "" + j);
                    threadTasks.Add(task);
                }

                Task.WaitAll(threadTasks.ToArray());

                perThreadTimings[Thread.CurrentThread] = profiler.GetSession().FinishProfiling().ToList();
            });

            threads.Add(thread);
        }

        threads.ForEach(thread => thread.Start());
        threads.ForEach(thread => thread.Join());

        perThreadTimings.Count.Should().Be(16);
        perThreadTimings.All(kv => kv.Value.Count == 1000).Should().BeTrue();
    }

    [Fact]
    public async Task profiling_md_ex2_async()
    {
        Skip.UnlessLongRunning();
        await using var conn = Create();

        var profiler = new AsyncLocalProfiler();
        var prefix = Me();

        conn.RegisterProfiler(profiler.GetSession);

        var tasks = new List<Task>();

        var perThreadTimings = new ConcurrentBag<List<IProfiledCommand>>();

        for (var i = 0; i < 16; i++)
        {
            var db = conn.GetDatabase(i);

            var task = Task.Run(async () =>
            {
                for (var j = 0; j < 100; j++)
                {
                    await db.StringSetAsync(prefix + j, "" + j).ForAwait();
                }

                perThreadTimings.Add(profiler.GetSession().FinishProfiling().ToList());
            }, TestContext.Current.CancellationToken);

            tasks.Add(task);
        }

        var timeout = Task.Delay(10000, TestContext.Current.CancellationToken);
        var complete = Task.WhenAll(tasks);
        if (timeout == await Task.WhenAny(timeout, complete).ForAwait())
        {
            throw new TimeoutException();
        }

        perThreadTimings.Count.Should().Be(16);
        foreach (var item in perThreadTimings)
        {
            item.Count.Should().Be(100);
        }
    }
}

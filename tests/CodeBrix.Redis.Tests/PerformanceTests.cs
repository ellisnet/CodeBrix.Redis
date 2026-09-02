using System.Diagnostics;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(NonParallelCollection.Name)]
public class PerformanceTests(ITestOutputHelper output) : TestBase(output)
{
    //was previously: [Fact] public async Task verify_performance_improvement(). NOT PORTED.
    //Its whole substance was a timing comparison of this library's asynchronous API against
    //redis-sharp (Copyright 2010 Novell, Inc., Miguel de Icaza, "new BSD license"), which upstream
    //vendored into its test suite as Helpers/redis-sharp.cs. That file is not ported - a fourth
    //third-party licence for an off-by-default comparison against a 2010 client - and without the
    //sync half there is nothing left to compare, so the test went with it. See
    //THIRD-PARTY-NOTICES.txt section 1. The remaining test below covers sync-vs-async overhead
    //using this library on both sides.
    [Fact]
    public async Task basic_string_get_perf()
    {
        await using var conn = Create();

        RedisKey key = Me();
        var db = conn.GetDatabase();
        await db.StringSetAsync(key, "some value").ForAwait();

        // this is just to JIT everything before we try testing
        var syncVal = db.StringGet(key);
        var asyncVal = await db.StringGetAsync(key).ForAwait();

        var syncTimer = Stopwatch.StartNew();
        syncVal = db.StringGet(key);
        syncTimer.Stop();

        var asyncTimer = Stopwatch.StartNew();
        asyncVal = await db.StringGetAsync(key).ForAwait();
        asyncTimer.Stop();

        Log($"Sync: {syncTimer.ElapsedMilliseconds}; Async: {asyncTimer.ElapsedMilliseconds}");
        syncVal.Should().Be("some value");
        asyncVal.Should().Be("some value");
        // let's allow 20% async overhead
        // But with a floor, since the base can often be zero
        (asyncTimer.ElapsedMilliseconds <= System.Math.Max(syncTimer.ElapsedMilliseconds * 1.2M, 50)).Should().BeTrue();
    }
}

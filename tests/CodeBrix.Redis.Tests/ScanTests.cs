using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class ScanTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task keys_scan(bool supported)
    {
        NoConcurrentRuntime();

        string[]? disabledCommands = supported ? null : ["scan"];
        await using var conn = Create(disabledCommands: disabledCommands, allowAdmin: true);

        var dbId = TestConfig.GetDedicatedDB(conn);
        var db = conn.GetDatabase(dbId);
        var prefix = Me() + ":";
        var server = GetServer(conn);
        server.Protocol.Should().Be(TestContext.Current.GetProtocol());
        server.FlushDatabase(dbId);
        for (int i = 0; i < 100; i++)
        {
            db.StringSet(prefix + i, Guid.NewGuid().ToString(), flags: CommandFlags.FireAndForget);
        }
        var seq = server.Keys(dbId, pageSize: 50);
        var cur = seq as IScanningCursor;
        Assert.NotNull(cur);
        Log($"Cursor: {cur.Cursor}, PageOffset: {cur.PageOffset}, PageSize: {cur.PageSize}");
        cur.PageOffset.Should().Be(0);
        cur.Cursor.Should().Be(0);
        if (supported)
        {
            cur.PageSize.Should().Be(50);
        }
        else
        {
            cur.PageSize.Should().Be(int.MaxValue);
        }
        seq.Distinct().Count().Should().Be(100);
        seq.Distinct().Count().Should().Be(100);
        server.Keys(dbId, prefix + "*").Distinct().Count().Should().Be(100);
        // 7, 70, 71, ..., 79
        server.Keys(dbId, prefix + "7*").Distinct().Count().Should().Be(11);
    }

    [Fact]
    public async Task scans_i_scanning()
    {
        NoConcurrentRuntime();

        await using var conn = Create(allowAdmin: true);

        var prefix = Me() + Guid.NewGuid();
        var dbId = TestConfig.GetDedicatedDB(conn);
        var db = conn.GetDatabase(dbId);
        var server = GetServer(conn);
        server.FlushDatabase(dbId);
        for (int i = 0; i < 100; i++)
        {
            db.StringSet(prefix + i, Guid.NewGuid().ToString(), flags: CommandFlags.FireAndForget);
        }
        var seq = server.Keys(dbId, prefix + "*", pageSize: 15);
        using (var iter = seq.GetEnumerator())
        {
            IScanningCursor s0 = (IScanningCursor)seq, s1 = (IScanningCursor)iter;

            s0.PageSize.Should().Be(15);
            s1.PageSize.Should().Be(15);

            // start at zero
            s0.Cursor.Should().Be(0);
            s1.Cursor.Should().Be(s0.Cursor);

            for (int i = 0; i < 47; i++)
            {
                iter.MoveNext().Should().BeTrue();
            }

            // non-zero in the middle
            s0.Cursor.Should().NotBe(0);
            s1.Cursor.Should().Be(s0.Cursor);

            for (int i = 0; i < 53; i++)
            {
                iter.MoveNext().Should().BeTrue();
            }

            // zero "next" at the end
            iter.MoveNext().Should().BeFalse();
            s0.Cursor.Should().NotBe(0);
            s1.Cursor.Should().NotBe(0);
        }
    }

    [Fact]
    public async Task scan_resume()
    {
        NoConcurrentRuntime();

        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v2_8_0);

        var dbId = TestConfig.GetDedicatedDB(conn);
        var db = conn.GetDatabase(dbId);
        var prefix = Me();
        var server = GetServer(conn);
        server.FlushDatabase(dbId);
        int i;
        for (i = 0; i < 100; i++)
        {
            db.StringSet(prefix + ":" + i, Guid.NewGuid().ToString());
        }

        var expected = new HashSet<string?>();
        long snapCursor = 0;
        int snapOffset = 0, snapPageSize = 0;

        i = 0;
        var seq = server.Keys(dbId, prefix + ":*", pageSize: 15);
        foreach (var key in seq)
        {
            if (i == 57)
            {
                snapCursor = ((IScanningCursor)seq).Cursor;
                snapOffset = ((IScanningCursor)seq).PageOffset;
                snapPageSize = ((IScanningCursor)seq).PageSize;
                Log($"i: {i}, Cursor: {snapCursor}, Offset: {snapOffset}, PageSize: {snapPageSize}");
            }
            if (i >= 57)
            {
                expected.Add(key);
            }
            i++;
        }
        Log($"Expected: 43, Actual: {expected.Count}, Cursor: {snapCursor}, Offset: {snapOffset}, PageSize: {snapPageSize}");
        expected.Count.Should().Be(43);
        snapCursor.Should().NotBe(0);
        snapPageSize.Should().Be(15);

        // note: you might think that we can say "hmmm, 57 when using page-size 15 on an empty (flushed) db (so: no skipped keys); that'll be
        // offset 12 in the 4th page; you'd be wrong, though; page size doesn't *actually* mean page size; it is a rough analogue for
        // page size, with zero guarantees; in this particular test, the first page actually has 19 elements, for example. So: we cannot
        // make the following assertion:
        // Assert.Equal(12, snapOffset);
        seq = server.Keys(dbId, prefix + ":*", pageSize: 15, cursor: snapCursor, pageOffset: snapOffset);
        var seqCur = (IScanningCursor)seq;
        seqCur.Cursor.Should().Be(snapCursor);
        seqCur.PageSize.Should().Be(snapPageSize);
        seqCur.PageOffset.Should().Be(snapOffset);
        using (var iter = seq.GetEnumerator())
        {
            var iterCur = (IScanningCursor)iter;
            iterCur.Cursor.Should().Be(snapCursor);
            iterCur.PageOffset.Should().Be(snapOffset);
            seqCur.Cursor.Should().Be(snapCursor);
            seqCur.PageOffset.Should().Be(snapOffset);

            iter.MoveNext().Should().BeTrue();
            iterCur.Cursor.Should().Be(snapCursor);
            iterCur.PageOffset.Should().Be(snapOffset);
            seqCur.Cursor.Should().Be(snapCursor);
            seqCur.PageOffset.Should().Be(snapOffset);

            iter.MoveNext().Should().BeTrue();
            iterCur.Cursor.Should().Be(snapCursor);
            iterCur.PageOffset.Should().Be(snapOffset + 1);
            seqCur.Cursor.Should().Be(snapCursor);
            seqCur.PageOffset.Should().Be(snapOffset + 1);
        }

        int count = 0;
        foreach (var key in seq)
        {
            expected.Remove(key);
            count++;
        }
        expected.Should().BeEmpty();
        count.Should().Be(43);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task set_scan(bool supported)
    {
        string[]? disabledCommands = supported ? null : ["sscan"];

        await using var conn = Create(disabledCommands: disabledCommands);

        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.SetAdd(key, "a", CommandFlags.FireAndForget);
        db.SetAdd(key, "b", CommandFlags.FireAndForget);
        db.SetAdd(key, "c", CommandFlags.FireAndForget);
        var arr = db.SetScan(key).ToArray();
        arr.Length.Should().Be(3);
        arr.Should().Contain((RedisValue)"a");
        arr.Should().Contain((RedisValue)"b");
        arr.Should().Contain((RedisValue)"c");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task sorted_set_scan(bool supported)
    {
        string[]? disabledCommands = supported ? null : ["zscan"];

        await using var conn = Create(disabledCommands: disabledCommands);

        RedisKey key = Me() + supported;
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.SortedSetAdd(key, "a", 1, CommandFlags.FireAndForget);
        db.SortedSetAdd(key, "b", 2, CommandFlags.FireAndForget);
        db.SortedSetAdd(key, "c", 3, CommandFlags.FireAndForget);

        var arr = db.SortedSetScan(key).ToArray();
        arr.Length.Should().Be(3);
        arr.Any(x => x.Element == "a" && x.Score == 1).Should().BeTrue("a");
        arr.Any(x => x.Element == "b" && x.Score == 2).Should().BeTrue("b");
        arr.Any(x => x.Element == "c" && x.Score == 3).Should().BeTrue("c");

        var dictionary = arr.ToDictionary();
        dictionary["a"].Should().Be(1);
        dictionary["b"].Should().Be(2);
        dictionary["c"].Should().Be(3);

        var sDictionary = arr.ToStringDictionary();
        sDictionary["a"].Should().Be(1);
        sDictionary["b"].Should().Be(2);
        sDictionary["c"].Should().Be(3);

        var basic = db.SortedSetRangeByRankWithScores(key, order: Order.Ascending).ToDictionary();
        basic.Count.Should().Be(3);
        basic["a"].Should().Be(1);
        basic["b"].Should().Be(2);
        basic["c"].Should().Be(3);

        basic = db.SortedSetRangeByRankWithScores(key, order: Order.Descending).ToDictionary();
        basic.Count.Should().Be(3);
        basic["a"].Should().Be(1);
        basic["b"].Should().Be(2);
        basic["c"].Should().Be(3);

        var basicArr = db.SortedSetRangeByScoreWithScores(key, order: Order.Ascending);
        basicArr.Length.Should().Be(3);
        basicArr[0].Score.Should().Be(1);
        basicArr[1].Score.Should().Be(2);
        basicArr[2].Score.Should().Be(3);
        basic = basicArr.ToDictionary();
        basic.Count.Should().Be(3); // asc
        basic["a"].Should().Be(1);
        basic["b"].Should().Be(2);
        basic["c"].Should().Be(3);

        basicArr = db.SortedSetRangeByScoreWithScores(key, order: Order.Descending);
        basicArr.Length.Should().Be(3);
        basicArr[0].Score.Should().Be(3);
        basicArr[1].Score.Should().Be(2);
        basicArr[2].Score.Should().Be(1);
        basic = basicArr.ToDictionary();
        basic.Count.Should().Be(3); // desc
        basic["a"].Should().Be(1);
        basic["b"].Should().Be(2);
        basic["c"].Should().Be(3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task hash_scan(bool supported)
    {
        string[]? disabledCommands = supported ? null : ["hscan"];

        await using var conn = Create(disabledCommands: disabledCommands);

        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.HashSet(key, "a", "1", flags: CommandFlags.FireAndForget);
        db.HashSet(key, "b", "2", flags: CommandFlags.FireAndForget);
        db.HashSet(key, "c", "3", flags: CommandFlags.FireAndForget);

        var arr = db.HashScan(key).ToArray();
        arr.Length.Should().Be(3);
        arr.Any(x => x.Name == "a" && x.Value == "1").Should().BeTrue("a");
        arr.Any(x => x.Name == "b" && x.Value == "2").Should().BeTrue("b");
        arr.Any(x => x.Name == "c" && x.Value == "3").Should().BeTrue("c");

        var dictionary = arr.ToDictionary();
        ((long)dictionary["a"]).Should().Be(1);
        ((long)dictionary["b"]).Should().Be(2);
        ((long)dictionary["c"]).Should().Be(3);

        var sDictionary = arr.ToStringDictionary();
        sDictionary["a"].Should().Be("1");
        sDictionary["b"].Should().Be("2");
        sDictionary["c"].Should().Be("3");

        var basic = db.HashGetAll(key).ToDictionary();
        basic.Count.Should().Be(3);
        ((long)basic["a"]).Should().Be(1);
        ((long)basic["b"]).Should().Be(2);
        ((long)basic["c"]).Should().Be(3);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public async Task hash_scan_large(int pageSize)
    {
        await using var conn = Create();

        RedisKey key = Me() + pageSize;
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        for (int i = 0; i < 2000; i++)
            db.HashSet(key, "k" + i, "v" + i, flags: CommandFlags.FireAndForget);

        int count = db.HashScan(key, pageSize: pageSize).Count();
        count.Should().Be(2000);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task hash_scan_no_values(bool supported)
    {
        string[]? disabledCommands = supported ? null : ["hscan"];

        await using var conn = Create(require: RedisFeatures.v7_4_0_rc1, disabledCommands: disabledCommands);

        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.HashSet(key, "a", "1", flags: CommandFlags.FireAndForget);
        db.HashSet(key, "b", "2", flags: CommandFlags.FireAndForget);
        db.HashSet(key, "c", "3", flags: CommandFlags.FireAndForget);

        var arr = db.HashScanNoValues(key).ToArray();
        arr.Length.Should().Be(3);
        arr.Any(x => x == "a").Should().BeTrue("a");
        arr.Any(x => x == "b").Should().BeTrue("b");
        arr.Any(x => x == "c").Should().BeTrue("c");

        var basic = db.HashGetAll(key).ToDictionary();
        basic.Count.Should().Be(3);
        ((long)basic["a"]).Should().Be(1);
        ((long)basic["b"]).Should().Be(2);
        ((long)basic["c"]).Should().Be(3);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public async Task hash_scan_no_values_large(int pageSize)
    {
        await using var conn = Create(require: RedisFeatures.v7_4_0_rc1);

        RedisKey key = Me() + pageSize;
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        for (int i = 0; i < 2000; i++)
        {
            db.HashSet(key, "k" + i, "v" + i, flags: CommandFlags.FireAndForget);
        }

        int count = db.HashScanNoValues(key, pageSize: pageSize).Count();
        count.Should().Be(2000);
    }

    /// <summary>
    /// See <see href="https://github.com/StackExchange/StackExchange.Redis/issues/729"/>.
    /// </summary>
    [Fact]
    public async Task hash_scan_thresholds()
    {
        await using var conn = Create(allowAdmin: true);

        var config = conn.GetServer(conn.GetEndPoints(true)[0]).ConfigGet("hash-max-ziplist-entries").First();
        var threshold = int.Parse(config.Value);

        RedisKey key = Me();
        GotCursors(conn, key, threshold - 1).Should().BeFalse();
        GotCursors(conn, key, threshold + 1).Should().BeTrue();
    }

    private static bool GotCursors(IConnectionMultiplexer conn, RedisKey key, int count)
    {
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var entries = new HashEntry[count];
        for (var i = 0; i < count; i++)
        {
            entries[i] = new HashEntry("Item:" + i, i);
        }
        db.HashSet(key, entries, CommandFlags.FireAndForget);

        var found = false;
        var response = db.HashScan(key);
        var cursor = (IScanningCursor)response;
        foreach (var _ in response)
        {
            if (cursor.Cursor > 0)
            {
                found = true;
            }
        }
        return found;
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public async Task set_scan_large(int pageSize)
    {
        await using var conn = Create();

        RedisKey key = Me() + pageSize;
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        for (int i = 0; i < 2000; i++)
            db.SetAdd(key, "s" + i, flags: CommandFlags.FireAndForget);

        int count = db.SetScan(key, pageSize: pageSize).Count();
        count.Should().Be(2000);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public async Task sorted_set_scan_large(int pageSize)
    {
        await using var conn = Create();

        RedisKey key = Me() + pageSize;
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        for (int i = 0; i < 2000; i++)
            db.SortedSetAdd(key, "z" + i, i, flags: CommandFlags.FireAndForget);

        int count = db.SortedSetScan(key, pageSize: pageSize).Count();
        count.Should().Be(2000);
    }
}

using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class SortedSetTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    private static readonly SortedSetEntry[] entries =
    [
        new SortedSetEntry("a", 1),
        new SortedSetEntry("b", 2),
        new SortedSetEntry("c", 3),
        new SortedSetEntry("d", 4),
        new SortedSetEntry("e", 5),
        new SortedSetEntry("f", 6),
        new SortedSetEntry("g", 7),
        new SortedSetEntry("h", 8),
        new SortedSetEntry("i", 9),
        new SortedSetEntry("j", 10),
    ];

    private static readonly SortedSetEntry[] entriesPow2 =
    [
        new SortedSetEntry("a", 1),
        new SortedSetEntry("b", 2),
        new SortedSetEntry("c", 4),
        new SortedSetEntry("d", 8),
        new SortedSetEntry("e", 16),
        new SortedSetEntry("f", 32),
        new SortedSetEntry("g", 64),
        new SortedSetEntry("h", 128),
        new SortedSetEntry("i", 256),
        new SortedSetEntry("j", 512),
    ];

    private static readonly SortedSetEntry[] entriesPow3 =
    [
        new SortedSetEntry("a", 1),
        new SortedSetEntry("c", 4),
        new SortedSetEntry("e", 16),
        new SortedSetEntry("g", 64),
        new SortedSetEntry("i", 256),
    ];

    private static readonly SortedSetEntry[] lexEntries =
    [
        new SortedSetEntry("a", 0),
        new SortedSetEntry("b", 0),
        new SortedSetEntry("c", 0),
        new SortedSetEntry("d", 0),
        new SortedSetEntry("e", 0),
        new SortedSetEntry("f", 0),
        new SortedSetEntry("g", 0),
        new SortedSetEntry("h", 0),
        new SortedSetEntry("i", 0),
        new SortedSetEntry("j", 0),
    ];

    [Fact]
    public async Task sorted_set_combine()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key1 = Me();
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        db.SortedSetAdd(key1, entries);
        db.SortedSetAdd(key2, entriesPow3);

        var diff = db.SortedSetCombine(SetOperation.Difference, [key1, key2]);
        diff.Length.Should().Be(5);
        diff[0].Should().Be("b");

        var inter = db.SortedSetCombine(SetOperation.Intersect, [key1, key2]);
        inter.Length.Should().Be(5);
        inter[0].Should().Be("a");

        var union = db.SortedSetCombine(SetOperation.Union, [key1, key2]);
        union.Length.Should().Be(10);
        union[0].Should().Be("a");
    }

    [Fact]
    public async Task sorted_set_combine_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key1 = Me();
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        db.SortedSetAdd(key1, entries);
        db.SortedSetAdd(key2, entriesPow3);

        var diff = await db.SortedSetCombineAsync(SetOperation.Difference, [key1, key2]);
        diff.Length.Should().Be(5);
        diff[0].Should().Be("b");

        var inter = await db.SortedSetCombineAsync(SetOperation.Intersect, [key1, key2]);
        inter.Length.Should().Be(5);
        inter[0].Should().Be("a");

        var union = await db.SortedSetCombineAsync(SetOperation.Union, [key1, key2]);
        union.Length.Should().Be(10);
        union[0].Should().Be("a");
    }

    [Fact]
    public async Task sorted_set_combine_with_scores()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key1 = Me();
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        db.SortedSetAdd(key1, entries);
        db.SortedSetAdd(key2, entriesPow3);

        var diff = db.SortedSetCombineWithScores(SetOperation.Difference, [key1, key2]);
        diff.Length.Should().Be(5);
        diff[0].Should().Be(new SortedSetEntry("b", 2));

        var inter = db.SortedSetCombineWithScores(SetOperation.Intersect, [key1, key2]);
        inter.Length.Should().Be(5);
        inter[0].Should().Be(new SortedSetEntry("a", 2));

        var union = db.SortedSetCombineWithScores(SetOperation.Union, [key1, key2]);
        union.Length.Should().Be(10);
        union[0].Should().Be(new SortedSetEntry("a", 2));
    }

    [Fact]
    public async Task sorted_set_combine_with_scores_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key1 = Me();
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        db.SortedSetAdd(key1, entries);
        db.SortedSetAdd(key2, entriesPow3);

        var diff = await db.SortedSetCombineWithScoresAsync(SetOperation.Difference, [key1, key2]);
        diff.Length.Should().Be(5);
        diff[0].Should().Be(new SortedSetEntry("b", 2));

        var inter = await db.SortedSetCombineWithScoresAsync(SetOperation.Intersect, [key1, key2]);
        inter.Length.Should().Be(5);
        inter[0].Should().Be(new SortedSetEntry("a", 2));

        var union = await db.SortedSetCombineWithScoresAsync(SetOperation.Union, [key1, key2]);
        union.Length.Should().Be(10);
        union[0].Should().Be(new SortedSetEntry("a", 2));
    }

    [Fact]
    public async Task sorted_set_combine_and_store()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key1 = Me();
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        var destination = Me() + "dest";
        db.KeyDelete(destination, CommandFlags.FireAndForget);

        db.SortedSetAdd(key1, entries);
        db.SortedSetAdd(key2, entriesPow3);

        var diff = db.SortedSetCombineAndStore(SetOperation.Difference, destination, [key1, key2]);
        diff.Should().Be(5);

        var inter = db.SortedSetCombineAndStore(SetOperation.Intersect, destination, [key1, key2]);
        inter.Should().Be(5);

        var union = db.SortedSetCombineAndStore(SetOperation.Union, destination, [key1, key2]);
        union.Should().Be(10);
    }

    [Fact]
    public async Task sorted_set_combine_and_store_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key1 = Me();
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        var destination = Me() + "dest";
        db.KeyDelete(destination, CommandFlags.FireAndForget);

        db.SortedSetAdd(key1, entries);
        db.SortedSetAdd(key2, entriesPow3);

        var diff = await db.SortedSetCombineAndStoreAsync(SetOperation.Difference, destination, [key1, key2]);
        diff.Should().Be(5);

        var inter = await db.SortedSetCombineAndStoreAsync(SetOperation.Intersect, destination, [key1, key2]);
        inter.Should().Be(5);

        var union = await db.SortedSetCombineAndStoreAsync(SetOperation.Union, destination, [key1, key2]);
        union.Should().Be(10);
    }

    [Fact]
    public async Task sorted_set_combine_errors()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key1 = Me();
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        var destination = Me() + "dest";
        db.KeyDelete(destination, CommandFlags.FireAndForget);

        db.SortedSetAdd(key1, entries);
        db.SortedSetAdd(key2, entriesPow3);

        // ZDIFF can't be used with weights
        var ex = Assert.Throws<ArgumentException>(() => db.SortedSetCombine(SetOperation.Difference, [key1, key2], [1, 2]));
        ex.Message.Should().Be("ZDIFF cannot be used with weights or aggregation.");
        ex = Assert.Throws<ArgumentException>(() => db.SortedSetCombineWithScores(SetOperation.Difference, [key1, key2], [1, 2]));
        ex.Message.Should().Be("ZDIFF cannot be used with weights or aggregation.");
        ex = Assert.Throws<ArgumentException>(() => db.SortedSetCombineAndStore(SetOperation.Difference, destination, [key1, key2], [1, 2]));
        ex.Message.Should().Be("ZDIFFSTORE cannot be used with weights or aggregation.");
        // and Async...
        ex = await Assert.ThrowsAsync<ArgumentException>(() => db.SortedSetCombineAsync(SetOperation.Difference, [key1, key2], [1, 2]));
        ex.Message.Should().Be("ZDIFF cannot be used with weights or aggregation.");
        ex = await Assert.ThrowsAsync<ArgumentException>(() => db.SortedSetCombineWithScoresAsync(SetOperation.Difference, [key1, key2], [1, 2]));
        ex.Message.Should().Be("ZDIFF cannot be used with weights or aggregation.");
        ex = await Assert.ThrowsAsync<ArgumentException>(() => db.SortedSetCombineAndStoreAsync(SetOperation.Difference, destination, [key1, key2], [1, 2]));
        ex.Message.Should().Be("ZDIFFSTORE cannot be used with weights or aggregation.");

        // ZDIFF can't be used with aggregation
        ex = Assert.Throws<ArgumentException>(() => db.SortedSetCombine(SetOperation.Difference, [key1, key2], aggregate: Aggregate.Max));
        ex.Message.Should().Be("ZDIFF cannot be used with weights or aggregation.");
        ex = Assert.Throws<ArgumentException>(() => db.SortedSetCombineWithScores(SetOperation.Difference, [key1, key2], aggregate: Aggregate.Max));
        ex.Message.Should().Be("ZDIFF cannot be used with weights or aggregation.");
        ex = Assert.Throws<ArgumentException>(() => db.SortedSetCombineAndStore(SetOperation.Difference, destination, [key1, key2], aggregate: Aggregate.Max));
        ex.Message.Should().Be("ZDIFFSTORE cannot be used with weights or aggregation.");
        // and Async...
        ex = await Assert.ThrowsAsync<ArgumentException>(() => db.SortedSetCombineAsync(SetOperation.Difference, [key1, key2], aggregate: Aggregate.Max));
        ex.Message.Should().Be("ZDIFF cannot be used with weights or aggregation.");
        ex = await Assert.ThrowsAsync<ArgumentException>(() => db.SortedSetCombineWithScoresAsync(SetOperation.Difference, [key1, key2], aggregate: Aggregate.Max));
        ex.Message.Should().Be("ZDIFF cannot be used with weights or aggregation.");
        ex = await Assert.ThrowsAsync<ArgumentException>(() => db.SortedSetCombineAndStoreAsync(SetOperation.Difference, destination, [key1, key2], aggregate: Aggregate.Max));
        ex.Message.Should().Be("ZDIFFSTORE cannot be used with weights or aggregation.");

        // Too many weights
        ex = Assert.Throws<ArgumentException>(() => db.SortedSetCombine(SetOperation.Union, [key1, key2], [1, 2, 3]));
        ex.Message.Should().StartWith("Keys and weights should have the same number of elements.");
        ex = Assert.Throws<ArgumentException>(() => db.SortedSetCombineWithScores(SetOperation.Union, [key1, key2], [1, 2, 3]));
        ex.Message.Should().StartWith("Keys and weights should have the same number of elements.");
        ex = Assert.Throws<ArgumentException>(() => db.SortedSetCombineAndStore(SetOperation.Union, destination, [key1, key2], [1, 2, 3]));
        ex.Message.Should().StartWith("Keys and weights should have the same number of elements.");
        // and Async...
        ex = await Assert.ThrowsAsync<ArgumentException>(() => db.SortedSetCombineAsync(SetOperation.Union, [key1, key2], [1, 2, 3]));
        ex.Message.Should().StartWith("Keys and weights should have the same number of elements.");
        ex = await Assert.ThrowsAsync<ArgumentException>(() => db.SortedSetCombineWithScoresAsync(SetOperation.Union, [key1, key2], [1, 2, 3]));
        ex.Message.Should().StartWith("Keys and weights should have the same number of elements.");
        ex = await Assert.ThrowsAsync<ArgumentException>(() => db.SortedSetCombineAndStoreAsync(SetOperation.Union, destination, [key1, key2], [1, 2, 3]));
        ex.Message.Should().StartWith("Keys and weights should have the same number of elements.");
    }

    [Fact]
    public async Task sorted_set_intersection_length()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key1 = Me();
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        db.SortedSetAdd(key1, entries);
        db.SortedSetAdd(key2, entriesPow3);

        var inter = db.SortedSetIntersectionLength([key1, key2]);
        inter.Should().Be(5);

        // with limit
        inter = db.SortedSetIntersectionLength([key1, key2], 3);
        inter.Should().Be(3);
    }

    [Fact]
    public async Task sorted_set_intersection_length_async()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key1 = Me();
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);

        db.SortedSetAdd(key1, entries);
        db.SortedSetAdd(key2, entriesPow3);

        var inter = await db.SortedSetIntersectionLengthAsync([key1, key2]);
        inter.Should().Be(5);

        // with limit
        inter = await db.SortedSetIntersectionLengthAsync([key1, key2], 3);
        inter.Should().Be(3);
    }

    [Fact]
    public async Task sorted_set_combine_aggregate_count()
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);

        var db = conn.GetDatabase();
        var key1 = Me();
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        var destination = Me() + "dest";
        db.KeyDelete(destination, CommandFlags.FireAndForget);

        db.SortedSetAdd(key1, entries);
        db.SortedSetAdd(key2, entriesPow3);

        var inter = db.SortedSetCombineWithScores(SetOperation.Intersect, [key1, key2], aggregate: Aggregate.Count);
        inter.Length.Should().Be(5);
        inter[0].Should().Be(new SortedSetEntry("a", 2));
        inter[1].Should().Be(new SortedSetEntry("c", 2));
        inter[2].Should().Be(new SortedSetEntry("e", 2));
        inter[3].Should().Be(new SortedSetEntry("g", 2));
        inter[4].Should().Be(new SortedSetEntry("i", 2));

        var union = db.SortedSetCombineWithScores(SetOperation.Union, [key1, key2], aggregate: Aggregate.Count);
        union.Length.Should().Be(10);
        union[0].Should().Be(new SortedSetEntry("b", 1));
        union[1].Should().Be(new SortedSetEntry("d", 1));
        union[2].Should().Be(new SortedSetEntry("f", 1));
        union[3].Should().Be(new SortedSetEntry("h", 1));
        union[4].Should().Be(new SortedSetEntry("j", 1));
        union[5].Should().Be(new SortedSetEntry("a", 2));
        union[6].Should().Be(new SortedSetEntry("c", 2));
        union[7].Should().Be(new SortedSetEntry("e", 2));
        union[8].Should().Be(new SortedSetEntry("g", 2));
        union[9].Should().Be(new SortedSetEntry("i", 2));

        var stored = db.SortedSetCombineAndStore(SetOperation.Intersect, destination, [key1, key2], aggregate: Aggregate.Count);
        stored.Should().Be(5);
        db.SortedSetRangeByRankWithScores(destination).Should().Equal(inter);
    }

    [Fact]
    public async Task sorted_set_range_via_script()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);
        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.SortedSetAdd(key, entries, CommandFlags.FireAndForget);

        var result = db.ScriptEvaluate(script: "return redis.call('ZRANGE', KEYS[1], 0, -1, 'WITHSCORES')", keys: [key]);
        AssertFlatArrayEntries(result);
    }

    [Fact]
    public async Task sorted_set_range_via_execute()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);
        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.SortedSetAdd(key, entries, CommandFlags.FireAndForget);

        var result = db.Execute("ZRANGE", [key, 0, -1, "WITHSCORES"]);

        if (TestContext.Current.IsResp3())
        {
            AssertJaggedArrayEntries(result);
        }
        else
        {
            AssertFlatArrayEntries(result);
        }
    }

    private void AssertFlatArrayEntries(RedisResult result)
    {
        result.Resp2Type.Should().Be(ResultType.Array);
        ((int)result.Length).Should().Be(entries.Length * 2);
        int index = 0;
        foreach (var entry in entries)
        {
            var e = result[index++];
            e.Resp2Type.Should().Be(ResultType.BulkString);
            e.AsRedisValue().Should().Be(entry.Element);

            e = result[index++];
            e.Resp2Type.Should().Be(ResultType.BulkString);
            e.AsDouble().Should().Be(entry.Score);
        }
    }

    private void AssertJaggedArrayEntries(RedisResult result)
    {
        result.Resp2Type.Should().Be(ResultType.Array);
        ((int)result.Length).Should().Be(entries.Length);
        int index = 0;
        foreach (var entry in entries)
        {
            var arr = result[index++];
            arr.Resp2Type.Should().Be(ResultType.Array);
            arr.Length.Should().Be(2);

            var e = arr[0];
            e.Resp2Type.Should().Be(ResultType.BulkString);
            e.AsRedisValue().Should().Be(entry.Element);

            e = arr[1];
            e.Resp2Type.Should().Be(ResultType.SimpleString);
            e.Resp3Type.Should().Be(ResultType.Double);
            e.AsDouble().Should().Be(entry.Score);
        }
    }

    [Fact]
    public async Task sorted_set_pop_multi_multi()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.SortedSetAdd(key, entries, CommandFlags.FireAndForget);

        var first = db.SortedSetPop(key, Order.Ascending);
        Assert.True(first.HasValue); //kept as the xUnit form: it is annotated so the compiler's null-state flows to the use below
        first.Value.Should().Be(entries[0]);
        db.SortedSetLength(key).Should().Be(9);

        var lasts = db.SortedSetPop(key, 2, Order.Descending);
        lasts.Length.Should().Be(2);
        lasts[0].Should().Be(entries[9]);
        lasts[1].Should().Be(entries[8]);
        db.SortedSetLength(key).Should().Be(7);
    }

    [Fact]
    public async Task sorted_set_pop_multi_single()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.SortedSetAdd(key, entries, CommandFlags.FireAndForget);

        var last = db.SortedSetPop(key, Order.Descending);
        Assert.True(last.HasValue); //kept as the xUnit form: it is annotated so the compiler's null-state flows to the use below
        last.Value.Should().Be(entries[9]);
        db.SortedSetLength(key).Should().Be(9);

        var firsts = db.SortedSetPop(key, 1, Order.Ascending);
        firsts.Should().ContainSingle();
        firsts[0].Should().Be(entries[0]);
        db.SortedSetLength(key).Should().Be(8);
    }

    [Fact]
    public async Task sorted_set_pop_multi_multi_async()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.SortedSetAdd(key, entries, CommandFlags.FireAndForget);

        var last = await db.SortedSetPopAsync(key, Order.Descending).ForAwait();
        Assert.True(last.HasValue); //kept as the xUnit form: it is annotated so the compiler's null-state flows to the use below
        last.Value.Should().Be(entries[9]);
        db.SortedSetLength(key).Should().Be(9);

        var moreLasts = await db.SortedSetPopAsync(key, 2, Order.Descending).ForAwait();
        moreLasts.Length.Should().Be(2);
        moreLasts[0].Should().Be(entries[8]);
        moreLasts[1].Should().Be(entries[7]);
        db.SortedSetLength(key).Should().Be(7);
    }

    [Fact]
    public async Task sorted_set_pop_multi_single_async()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.SortedSetAdd(key, entries, CommandFlags.FireAndForget);

        var first = await db.SortedSetPopAsync(key).ForAwait();
        Assert.True(first.HasValue); //kept as the xUnit form: it is annotated so the compiler's null-state flows to the use below
        first.Value.Should().Be(entries[0]);
        db.SortedSetLength(key).Should().Be(9);

        var moreFirsts = await db.SortedSetPopAsync(key, 1).ForAwait();
        moreFirsts.Should().ContainSingle();
        moreFirsts[0].Should().Be(entries[1]);
        db.SortedSetLength(key).Should().Be(8);
    }

    [Fact]
    public async Task sorted_set_pop_multi_zero_async()
    {
        await using var conn = Create(require: RedisFeatures.v5_0_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.SortedSetAdd(key, entries, CommandFlags.FireAndForget);

        var t = db.SortedSetPopAsync(key, count: 0);
        t.IsCompleted.Should().BeTrue(); // sync
        var arr = await t;
        Assert.NotNull(arr);
        arr.Should().BeEmpty();

        db.SortedSetLength(key).Should().Be(10);
    }

    [Fact]
    public async Task sorted_set_random_members()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        var key0 = Me() + "non-existing";

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key0, CommandFlags.FireAndForget);
        db.SortedSetAdd(key, entries, CommandFlags.FireAndForget);

        // single member
        var randMember = db.SortedSetRandomMember(key);
        Array.Exists(entries, element => element.Element.Equals(randMember)).Should().BeTrue();

        // with count
        var randMemberArray = db.SortedSetRandomMembers(key, 5);
        randMemberArray.Length.Should().Be(5);
        randMemberArray = db.SortedSetRandomMembers(key, 15);
        randMemberArray.Length.Should().Be(10);
        randMemberArray = db.SortedSetRandomMembers(key, -5);
        randMemberArray.Length.Should().Be(5);
        randMemberArray = db.SortedSetRandomMembers(key, -15);
        randMemberArray.Length.Should().Be(15);

        // with scores
        var randMemberArray2 = db.SortedSetRandomMembersWithScores(key, 2);
        randMemberArray2.Length.Should().Be(2);
        foreach (var member in randMemberArray2)
        {
            entries.Should().Contain(member);
        }

        // check missing key case
        randMember = db.SortedSetRandomMember(key0);
        randMember.IsNull.Should().BeTrue();
        randMemberArray = db.SortedSetRandomMembers(key0, 2);
        (randMemberArray.Length == 0).Should().BeTrue();
        randMemberArray2 = db.SortedSetRandomMembersWithScores(key0, 2);
        (randMemberArray2.Length == 0).Should().BeTrue();
    }

    [Fact]
    public async Task sorted_set_random_members_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        var key0 = Me() + "non-existing";

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.KeyDelete(key0, CommandFlags.FireAndForget);
        db.SortedSetAdd(key, entries, CommandFlags.FireAndForget);

        var randMember = await db.SortedSetRandomMemberAsync(key);
        Array.Exists(entries, element => element.Element.Equals(randMember)).Should().BeTrue();

        // with count
        var randMemberArray = await db.SortedSetRandomMembersAsync(key, 5);
        randMemberArray.Length.Should().Be(5);
        randMemberArray = await db.SortedSetRandomMembersAsync(key, 15);
        randMemberArray.Length.Should().Be(10);
        randMemberArray = await db.SortedSetRandomMembersAsync(key, -5);
        randMemberArray.Length.Should().Be(5);
        randMemberArray = await db.SortedSetRandomMembersAsync(key, -15);
        randMemberArray.Length.Should().Be(15);

        // with scores
        var randMemberArray2 = await db.SortedSetRandomMembersWithScoresAsync(key, 2);
        randMemberArray2.Length.Should().Be(2);
        foreach (var member in randMemberArray2)
        {
            entries.Should().Contain(member);
        }

        // check missing key case
        randMember = await db.SortedSetRandomMemberAsync(key0);
        randMember.IsNull.Should().BeTrue();
        randMemberArray = await db.SortedSetRandomMembersAsync(key0, 2);
        (randMemberArray.Length == 0).Should().BeTrue();
        randMemberArray2 = await db.SortedSetRandomMembersWithScoresAsync(key0, 2);
        (randMemberArray2.Length == 0).Should().BeTrue();
    }

    [Fact]
    public async Task sorted_set_range_store_by_rank_async()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        await db.SortedSetAddAsync(sourceKey, entries, CommandFlags.FireAndForget);

        //Act
        var res = await db.SortedSetRangeAndStoreAsync(sourceKey, destinationKey, 0, -1);

        //Assert
        res.Should().Be(entries.Length);
    }

    [Fact]
    public async Task sorted_set_range_store_by_rank_limited_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        await db.SortedSetAddAsync(sourceKey, entries, CommandFlags.FireAndForget);
        var res = await db.SortedSetRangeAndStoreAsync(sourceKey, destinationKey, 1, 4);
        var range = await db.SortedSetRangeByRankWithScoresAsync(destinationKey);
        res.Should().Be(4);
        for (var i = 1; i < 5; i++)
        {
            range[i - 1].Should().Be(entries[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_score_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        await db.SortedSetAddAsync(sourceKey, entriesPow2, CommandFlags.FireAndForget);
        var res = await db.SortedSetRangeAndStoreAsync(sourceKey, destinationKey, 64, 128, SortedSetOrder.ByScore);
        var range = await db.SortedSetRangeByRankWithScoresAsync(destinationKey);
        res.Should().Be(2);
        for (var i = 6; i < 8; i++)
        {
            range[i - 6].Should().Be(entriesPow2[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_score_async_default()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        await db.SortedSetAddAsync(sourceKey, entriesPow2, CommandFlags.FireAndForget);
        var res = await db.SortedSetRangeAndStoreAsync(sourceKey, destinationKey, double.NegativeInfinity, double.PositiveInfinity, SortedSetOrder.ByScore);
        var range = await db.SortedSetRangeByRankWithScoresAsync(destinationKey);
        res.Should().Be(10);
        for (var i = 0; i < entriesPow2.Length; i++)
        {
            range[i].Should().Be(entriesPow2[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_score_async_limited()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        await db.SortedSetAddAsync(sourceKey, entriesPow2, CommandFlags.FireAndForget);
        var res = await db.SortedSetRangeAndStoreAsync(sourceKey, destinationKey, double.NegativeInfinity, double.PositiveInfinity, SortedSetOrder.ByScore, skip: 1, take: 6);
        var range = await db.SortedSetRangeByRankWithScoresAsync(destinationKey);
        res.Should().Be(6);
        for (var i = 1; i < 7; i++)
        {
            range[i - 1].Should().Be(entriesPow2[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_score_async_exclusive_range()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        await db.SortedSetAddAsync(sourceKey, entriesPow2, CommandFlags.FireAndForget);
        var res = await db.SortedSetRangeAndStoreAsync(sourceKey, destinationKey, 32, 256, SortedSetOrder.ByScore, exclude: Exclude.Both);
        var range = await db.SortedSetRangeByRankWithScoresAsync(destinationKey);
        res.Should().Be(2);
        for (var i = 6; i < 8; i++)
        {
            range[i - 6].Should().Be(entriesPow2[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_score_async_reverse()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        await db.SortedSetAddAsync(sourceKey, entriesPow2, CommandFlags.FireAndForget);
        var res = await db.SortedSetRangeAndStoreAsync(sourceKey, destinationKey, start: double.PositiveInfinity, double.NegativeInfinity, SortedSetOrder.ByScore, order: Order.Descending);
        var range = await db.SortedSetRangeByRankWithScoresAsync(destinationKey);
        res.Should().Be(10);
        for (var i = 0; i < entriesPow2.Length; i++)
        {
            range[i].Should().Be(entriesPow2[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_lex_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        await db.SortedSetAddAsync(sourceKey, lexEntries, CommandFlags.FireAndForget);
        var res = await db.SortedSetRangeAndStoreAsync(sourceKey, destinationKey, "a", "j", SortedSetOrder.ByLex);
        var range = await db.SortedSetRangeByRankWithScoresAsync(destinationKey);
        res.Should().Be(10);
        for (var i = 0; i < lexEntries.Length; i++)
        {
            range[i].Should().Be(lexEntries[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_lex_exclusive_range_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        await db.SortedSetAddAsync(sourceKey, lexEntries, CommandFlags.FireAndForget);
        var res = await db.SortedSetRangeAndStoreAsync(sourceKey, destinationKey, "a", "j", SortedSetOrder.ByLex, Exclude.Both);
        var range = await db.SortedSetRangeByRankWithScoresAsync(destinationKey);
        res.Should().Be(8);
        for (var i = 1; i < lexEntries.Length - 1; i++)
        {
            range[i - 1].Should().Be(lexEntries[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_lex_rev_range_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        await db.SortedSetAddAsync(sourceKey, lexEntries, CommandFlags.FireAndForget);
        var res = await db.SortedSetRangeAndStoreAsync(sourceKey, destinationKey, "j", "a", SortedSetOrder.ByLex, exclude: Exclude.None, order: Order.Descending);
        var range = await db.SortedSetRangeByRankWithScoresAsync(destinationKey);
        res.Should().Be(10);
        for (var i = 0; i < lexEntries.Length; i++)
        {
            range[i].Should().Be(lexEntries[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_rank()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        db.SortedSetAdd(sourceKey, entries, CommandFlags.FireAndForget);

        //Act
        var res = db.SortedSetRangeAndStore(sourceKey, destinationKey, 0, -1);

        //Assert
        res.Should().Be(entries.Length);
    }

    [Fact]
    public async Task sorted_set_range_store_by_rank_limited()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        db.SortedSetAdd(sourceKey, entries, CommandFlags.FireAndForget);
        var res = db.SortedSetRangeAndStore(sourceKey, destinationKey, 1, 4);
        var range = db.SortedSetRangeByRankWithScores(destinationKey);
        res.Should().Be(4);
        for (var i = 1; i < 5; i++)
        {
            range[i - 1].Should().Be(entries[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_score()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        db.SortedSetAdd(sourceKey, entriesPow2, CommandFlags.FireAndForget);
        var res = db.SortedSetRangeAndStore(sourceKey, destinationKey, 64, 128, SortedSetOrder.ByScore);
        var range = db.SortedSetRangeByRankWithScores(destinationKey);
        res.Should().Be(2);
        for (var i = 6; i < 8; i++)
        {
            range[i - 6].Should().Be(entriesPow2[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_score_default()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        db.SortedSetAdd(sourceKey, entriesPow2, CommandFlags.FireAndForget);
        var res = db.SortedSetRangeAndStore(sourceKey, destinationKey, double.NegativeInfinity, double.PositiveInfinity, SortedSetOrder.ByScore);
        var range = db.SortedSetRangeByRankWithScores(destinationKey);
        res.Should().Be(10);
        for (var i = 0; i < entriesPow2.Length; i++)
        {
            range[i].Should().Be(entriesPow2[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_score_limited()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        db.SortedSetAdd(sourceKey, entriesPow2, CommandFlags.FireAndForget);
        var res = db.SortedSetRangeAndStore(sourceKey, destinationKey, double.NegativeInfinity, double.PositiveInfinity, SortedSetOrder.ByScore, skip: 1, take: 6);
        var range = db.SortedSetRangeByRankWithScores(destinationKey);
        res.Should().Be(6);
        for (var i = 1; i < 7; i++)
        {
            range[i - 1].Should().Be(entriesPow2[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_score_exclusive_range()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        db.SortedSetAdd(sourceKey, entriesPow2, CommandFlags.FireAndForget);
        var res = db.SortedSetRangeAndStore(sourceKey, destinationKey, 32, 256, SortedSetOrder.ByScore, exclude: Exclude.Both);
        var range = db.SortedSetRangeByRankWithScores(destinationKey);
        res.Should().Be(2);
        for (var i = 6; i < 8; i++)
        {
            range[i - 6].Should().Be(entriesPow2[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_score_reverse()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        db.SortedSetAdd(sourceKey, entriesPow2, CommandFlags.FireAndForget);
        var res = db.SortedSetRangeAndStore(sourceKey, destinationKey, start: double.PositiveInfinity, double.NegativeInfinity, SortedSetOrder.ByScore, order: Order.Descending);
        var range = db.SortedSetRangeByRankWithScores(destinationKey);
        res.Should().Be(10);
        for (var i = 0; i < entriesPow2.Length; i++)
        {
            range[i].Should().Be(entriesPow2[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_lex()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        db.SortedSetAdd(sourceKey, lexEntries, CommandFlags.FireAndForget);
        var res = db.SortedSetRangeAndStore(sourceKey, destinationKey, "a", "j", SortedSetOrder.ByLex);
        var range = db.SortedSetRangeByRankWithScores(destinationKey);
        res.Should().Be(10);
        for (var i = 0; i < lexEntries.Length; i++)
        {
            range[i].Should().Be(lexEntries[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_lex_exclusive_range()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        db.SortedSetAdd(sourceKey, lexEntries, CommandFlags.FireAndForget);
        var res = db.SortedSetRangeAndStore(sourceKey, destinationKey, "a", "j", SortedSetOrder.ByLex, Exclude.Both);
        var range = db.SortedSetRangeByRankWithScores(destinationKey);
        res.Should().Be(8);
        for (var i = 1; i < lexEntries.Length - 1; i++)
        {
            range[i - 1].Should().Be(lexEntries[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_by_lex_rev_range()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);
        db.SortedSetAdd(sourceKey, lexEntries, CommandFlags.FireAndForget);
        var res = db.SortedSetRangeAndStore(sourceKey, destinationKey, "j", "a", SortedSetOrder.ByLex, Exclude.None, Order.Descending);
        var range = db.SortedSetRangeByRankWithScores(destinationKey);
        res.Should().Be(10);
        for (var i = 0; i < lexEntries.Length; i++)
        {
            range[i].Should().Be(lexEntries[i]);
        }
    }

    [Fact]
    public async Task sorted_set_range_store_fail_erroneous_take()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);

        //Act
        db.SortedSetAdd(sourceKey, lexEntries, CommandFlags.FireAndForget);

        //Assert
        var exception = Assert.Throws<ArgumentException>(() => db.SortedSetRangeAndStore(sourceKey, destinationKey, 0, -1, take: 5));
        exception.ParamName.Should().Be("take");
    }

    [Fact]
    public async Task sorted_set_range_store_fail_exclude()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);

        //Act
        db.SortedSetAdd(sourceKey, lexEntries, CommandFlags.FireAndForget);

        //Assert
        var exception = Assert.Throws<ArgumentException>(() => db.SortedSetRangeAndStore(sourceKey, destinationKey, 0, -1, exclude: Exclude.Both));
        exception.ParamName.Should().Be("exclude");
    }

    [Fact]
    public async Task sorted_set_multi_pop_single_key()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key);

        db.SortedSetAdd(
            key,
            [
                new SortedSetEntry("rays", 100),
                new SortedSetEntry("yankees", 92),
                new SortedSetEntry("red sox", 92),
                new SortedSetEntry("blue jays", 91),
                new SortedSetEntry("orioles", 52),
            ]);

        var highest = db.SortedSetPop([key], 1, order: Order.Descending);
        highest.IsNull.Should().BeFalse();
        highest.Key.Should().Be(key);
        var entry = Assert.Single(highest.Entries);
        entry.Element.Should().Be("rays");
        entry.Score.Should().Be(100);

        var bottom2 = db.SortedSetPop([key], 2);
        bottom2.IsNull.Should().BeFalse();
        bottom2.Key.Should().Be(key);
        bottom2.Entries.Length.Should().Be(2);
        bottom2.Entries[0].Element.Should().Be("orioles");
        bottom2.Entries[0].Score.Should().Be(52);
        bottom2.Entries[1].Element.Should().Be("blue jays");
        bottom2.Entries[1].Score.Should().Be(91);
    }

    [Fact]
    public async Task sorted_set_multi_pop_multi_key()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        RedisKey[] keys = [key + ":missing1", key, key + ":missing2"];
        db.KeyDelete(keys);

        db.SortedSetAdd(
            key,
            [
                new SortedSetEntry("rays", 100),
                new SortedSetEntry("yankees", 92),
                new SortedSetEntry("red sox", 92),
                new SortedSetEntry("blue jays", 91),
                new SortedSetEntry("orioles", 52),
            ]);

        var highest = db.SortedSetPop(keys, 1, order: Order.Descending);
        highest.IsNull.Should().BeFalse();
        highest.Key.Should().Be(key);
        var entry = Assert.Single(highest.Entries);
        entry.Element.Should().Be("rays");
        entry.Score.Should().Be(100);

        var bottom2 = db.SortedSetPop(keys, 2);
        bottom2.IsNull.Should().BeFalse();
        bottom2.Key.Should().Be(key);
        bottom2.Entries.Length.Should().Be(2);
        bottom2.Entries[0].Element.Should().Be("orioles");
        bottom2.Entries[0].Score.Should().Be(52);
        bottom2.Entries[1].Element.Should().Be("blue jays");
        bottom2.Entries[1].Score.Should().Be(91);
    }

    [Fact]
    public async Task sorted_set_multi_pop_no_set()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        RedisKey[] keys = [key + ":missing1", key, key + ":missing2"];
        db.KeyDelete(keys);

        //Act
        var res = db.SortedSetPop([key], 1);

        //Assert
        res.IsNull.Should().BeTrue();
    }

    [Fact]
    public async Task sorted_set_multi_pop_count0()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();

        //Act
        db.KeyDelete(key);

        //Assert
        var exception = Assert.Throws<RedisServerException>(() => db.SortedSetPop([key], 0));
        exception.Message.Should().Contain("ERR count should be greater than 0");
    }

    [Fact]
    public async Task sorted_set_multi_pop_async()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        RedisKey[] keys = [key + ":missing1", key, key + ":missing2"];
        db.KeyDelete(keys);

        db.SortedSetAdd(
            key,
            [
                new SortedSetEntry("rays", 100),
                new SortedSetEntry("yankees", 92),
                new SortedSetEntry("red sox", 92),
                new SortedSetEntry("blue jays", 91),
                new SortedSetEntry("orioles", 52),
            ]);

        var highest = await db.SortedSetPopAsync(
            keys, 1, order: Order.Descending);
        highest.IsNull.Should().BeFalse();
        highest.Key.Should().Be(key);
        var entry = Assert.Single(highest.Entries);
        entry.Element.Should().Be("rays");
        entry.Score.Should().Be(100);

        var bottom2 = await db.SortedSetPopAsync(keys, 2);
        bottom2.IsNull.Should().BeFalse();
        bottom2.Key.Should().Be(key);
        bottom2.Entries.Length.Should().Be(2);
        bottom2.Entries[0].Element.Should().Be("orioles");
        bottom2.Entries[0].Score.Should().Be(52);
        bottom2.Entries[1].Element.Should().Be("blue jays");
        bottom2.Entries[1].Score.Should().Be(91);
    }

    [Fact]
    public async Task sorted_set_multi_pop_empty_keys()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        //Act
        var db = conn.GetDatabase();

        //Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => db.SortedSetPop(Array.Empty<RedisKey>(), 5));
        exception.Message.Should().Contain("keys must have a size of at least 1");
    }

    [Fact]
    public async Task sorted_set_range_store_fail_for_replica()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var me = Me();
        var sourceKey = $"{me}:ZSetSource";
        var destinationKey = $"{me}:ZSetDestination";

        db.KeyDelete([sourceKey, destinationKey], CommandFlags.FireAndForget);

        //Act
        db.SortedSetAdd(sourceKey, lexEntries, CommandFlags.FireAndForget);

        //Assert
        var exception = Assert.Throws<RedisCommandException>(() => db.SortedSetRangeAndStore(sourceKey, destinationKey, 0, -1, flags: CommandFlags.DemandReplica));
        exception.Message.Should().Contain("Command cannot be issued to a replica");
    }

    [Fact]
    public async Task sorted_set_scores_single()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v2_1_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string memberName = "member";

        db.KeyDelete(key);
        db.SortedSetAdd(key, memberName, 1.5);

        //Act
        var score = db.SortedSetScore(key, memberName);

        //Assert
        Assert.NotNull(score);
        score.Should().Be((double)1.5);
    }

    [Fact]
    public async Task sorted_set_scores_single_async()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v2_1_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string memberName = "member";

        await db.KeyDeleteAsync(key);
        await db.SortedSetAddAsync(key, memberName, 1.5);

        //Act
        var score = await db.SortedSetScoreAsync(key, memberName);

        //Assert
        Assert.NotNull(score);
        score.Value.Should().Be((double)1.5);
    }

    [Fact]
    public async Task sorted_set_scores_single_missing_set_still_returns_null()
    {
        await using var conn = Create(require: RedisFeatures.v2_1_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key);

        // Attempt to retrieve score for a missing set, should still return null.
        var score = db.SortedSetScore(key, "bogusMemberName");

        score.Should().BeNull();
    }

    [Fact]
    public async Task sorted_set_scores_single_missing_set_still_returns_null_async()
    {
        await using var conn = Create(require: RedisFeatures.v2_1_0);

        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key);

        // Attempt to retrieve score for a missing set, should still return null.
        var score = await db.SortedSetScoreAsync(key, "bogusMemberName");

        score.Should().BeNull();
    }

    [Fact]
    public async Task sorted_set_scores_single_returns_null_for_missing_member()
    {
        await using var conn = Create(require: RedisFeatures.v2_1_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key);
        db.SortedSetAdd(key, "member1", 1.5);

        // Attempt to retrieve score for a missing member, should return null.
        var score = db.SortedSetScore(key, "bogusMemberName");

        score.Should().BeNull();
    }

    [Fact]
    public async Task sorted_set_scores_single_returns_null_for_missing_member_async()
    {
        await using var conn = Create(require: RedisFeatures.v2_1_0);

        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key);
        await db.SortedSetAddAsync(key, "member1", 1.5);

        // Attempt to retrieve score for a missing member, should return null.
        var score = await db.SortedSetScoreAsync(key, "bogusMemberName");

        score.Should().BeNull();
    }

    [Fact]
    public async Task sorted_set_scores_multiple()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string member1 = "member1",
                     member2 = "member2",
                     member3 = "member3";

        db.KeyDelete(key);
        db.SortedSetAdd(key, member1, 1.5);
        db.SortedSetAdd(key, member2, 1.75);
        db.SortedSetAdd(key, member3, 2);

        //Act
        var scores = db.SortedSetScores(key, [member1, member2, member3]);

        //Assert
        Assert.NotNull(scores);
        scores.Length.Should().Be(3);
        scores[0].Should().Be((double)1.5);
        scores[1].Should().Be((double)1.75);
        scores[2].Should().Be(2);
    }

    [Fact]
    public async Task sorted_set_scores_multiple_async()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string member1 = "member1",
                     member2 = "member2",
                     member3 = "member3";

        await db.KeyDeleteAsync(key);
        await db.SortedSetAddAsync(key, member1, 1.5);
        await db.SortedSetAddAsync(key, member2, 1.75);
        await db.SortedSetAddAsync(key, member3, 2);

        //Act
        var scores = await db.SortedSetScoresAsync(key, [member1, member2, member3]);

        //Assert
        Assert.NotNull(scores);
        scores.Length.Should().Be(3);
        scores[0].Should().Be((double)1.5);
        scores[1].Should().Be((double)1.75);
        scores[2].Should().Be(2);
    }

    [Fact]
    public async Task sorted_set_scores_multiple_returns_null_items_for_missing_set()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key);

        // Missing set but should still return an array of nulls.
        var scores = db.SortedSetScores(key, ["bogus1", "bogus2", "bogus3"]);

        Assert.NotNull(scores);
        scores.Length.Should().Be(3);
        scores[0].Should().BeNull();
        scores[1].Should().BeNull();
        scores[2].Should().BeNull();
    }

    [Fact]
    public async Task sorted_set_scores_multiple_returns_null_items_for_missing_set_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();

        await db.KeyDeleteAsync(key);

        // Missing set but should still return an array of nulls.
        var scores = await db.SortedSetScoresAsync(key, ["bogus1", "bogus2", "bogus3"]);

        Assert.NotNull(scores);
        scores.Length.Should().Be(3);
        scores[0].Should().BeNull();
        scores[1].Should().BeNull();
        scores[2].Should().BeNull();
    }

    [Fact]
    public async Task sorted_set_scores_multiple_returns_scores_and_null_items()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string member1 = "member1",
                     member2 = "member2",
                     member3 = "member3",
                     bogusMember = "bogusMember";

        db.KeyDelete(key);

        db.SortedSetAdd(key, member1, 1.5);
        db.SortedSetAdd(key, member2, 1.75);
        db.SortedSetAdd(key, member3, 2);

        //Act
        var scores = db.SortedSetScores(key, [member1, bogusMember, member2, member3]);

        //Assert
        Assert.NotNull(scores);
        scores.Length.Should().Be(4);
        scores[1].Should().BeNull();
        scores[0].Should().Be((double)1.5);
        scores[2].Should().Be((double)1.75);
        scores[3].Should().Be(2);
    }

    [Fact]
    public async Task sorted_set_scores_multiple_returns_scores_and_null_items_async()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var key = Me();
        const string member1 = "member1",
                     member2 = "member2",
                     member3 = "member3",
                     bogusMember = "bogusMember";

        await db.KeyDeleteAsync(key);

        await db.SortedSetAddAsync(key, member1, 1.5);
        await db.SortedSetAddAsync(key, member2, 1.75);
        await db.SortedSetAddAsync(key, member3, 2);

        //Act
        var scores = await db.SortedSetScoresAsync(key, [member1, bogusMember, member2, member3]);

        //Assert
        Assert.NotNull(scores);
        scores.Length.Should().Be(4);
        scores[1].Should().BeNull();
        scores[0].Should().Be((double)1.5);
        scores[2].Should().Be((double)1.75);
        scores[3].Should().Be(2);
    }

    [Fact]
    public async Task sorted_set_update()
    {
        await using var conn = Create(require: RedisFeatures.v3_0_0);

        var db = conn.GetDatabase();
        var key = Me();
        var member = "a";
        var values = new SortedSetEntry[] { new SortedSetEntry(member, 5) };
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.SortedSetAdd(key, member, 2);

        db.SortedSetUpdate(key, member, 1).Should().BeTrue();
        db.SortedSetUpdate(key, values).Should().Be(1);

        (await db.SortedSetUpdateAsync(key, member, 1)).Should().BeTrue();
        (await db.SortedSetUpdateAsync(key, values)).Should().Be(1);
    }
}

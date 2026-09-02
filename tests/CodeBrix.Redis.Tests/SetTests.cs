using System;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class SetTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task set_contains()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key);
        for (int i = 1; i < 1001; i++)
        {
            db.SetAdd(key, i, CommandFlags.FireAndForget);
        }

        // Single member
        var isMemeber = db.SetContains(key, 1);
        isMemeber.Should().BeTrue();

        // Multi members
        var areMemebers = db.SetContains(key, [0, 1, 2]);
        areMemebers.Length.Should().Be(3);
        areMemebers[0].Should().BeFalse();
        areMemebers[1].Should().BeTrue();

        // key not exists
        db.KeyDelete(key);
        isMemeber = db.SetContains(key, 1);
        isMemeber.Should().BeFalse();
        areMemebers = db.SetContains(key, [0, 1, 2]);
        areMemebers.Length.Should().Be(3);
        areMemebers.All(i => !i).Should().BeTrue(); // Check that all the elements are False
    }

    [Fact]
    public async Task set_contains_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        await db.KeyDeleteAsync(key);
        for (int i = 1; i < 1001; i++)
        {
            db.SetAdd(key, i, CommandFlags.FireAndForget);
        }

        // Single member
        var isMemeber = await db.SetContainsAsync(key, 1);
        isMemeber.Should().BeTrue();

        // Multi members
        var areMemebers = await db.SetContainsAsync(key, [0, 1, 2]);
        areMemebers.Length.Should().Be(3);
        areMemebers[0].Should().BeFalse();
        areMemebers[1].Should().BeTrue();

        // key not exists
        await db.KeyDeleteAsync(key);
        isMemeber = await db.SetContainsAsync(key, 1);
        isMemeber.Should().BeFalse();
        areMemebers = await db.SetContainsAsync(key, [0, 1, 2]);
        areMemebers.Length.Should().Be(3);
        areMemebers.All(i => !i).Should().BeTrue(); // Check that all the elements are False
    }

    [Fact]
    public async Task set_intersection_length()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();

        var key1 = Me() + "1";
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        db.SetAdd(key1, [0, 1, 2, 3, 4], CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        db.SetAdd(key2, [1, 2, 3, 4, 5], CommandFlags.FireAndForget);

        db.SetIntersectionLength([key1, key2]).Should().Be(4);
        // with limit
        db.SetIntersectionLength([key1, key2], 3).Should().Be(3);

        // Missing keys should be 0
        var key3 = Me() + "3";
        var key4 = Me() + "4";
        db.KeyDelete(key3, CommandFlags.FireAndForget);
        db.SetIntersectionLength([key1, key3]).Should().Be(0);
        db.SetIntersectionLength([key3, key4]).Should().Be(0);
    }

    [Fact]
    public async Task set_intersection_length_async()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();

        var key1 = Me() + "1";
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        db.SetAdd(key1, [0, 1, 2, 3, 4], CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        db.SetAdd(key2, [1, 2, 3, 4, 5], CommandFlags.FireAndForget);

        (await db.SetIntersectionLengthAsync([key1, key2])).Should().Be(4);
        // with limit
        (await db.SetIntersectionLengthAsync([key1, key2], 3)).Should().Be(3);

        // Missing keys should be 0
        var key3 = Me() + "3";
        var key4 = Me() + "4";
        db.KeyDelete(key3, CommandFlags.FireAndForget);
        (await db.SetIntersectionLengthAsync([key1, key3])).Should().Be(0);
        (await db.SetIntersectionLengthAsync([key3, key4])).Should().Be(0);
    }

    [Fact]
    public async Task set_combine_length_union()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);

        var db = conn.GetDatabase();

        var key1 = Me() + "1";
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        db.SetAdd(key1, [0, 1, 2, 3, 4], CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        db.SetAdd(key2, [3, 4, 5], CommandFlags.FireAndForget);

        // union = {0,1,2,3,4,5}
        db.SetCombineLength(SetOperation.Union, [key1, key2]).Should().Be(6);
        (await db.SetCombineLengthAsync(SetOperation.Union, [key1, key2])).Should().Be(6);
        // with limit
        db.SetCombineLength(SetOperation.Union, [key1, key2], 4).Should().Be(4);
        (await db.SetCombineLengthAsync(SetOperation.Union, [key1, key2], 4)).Should().Be(4);
        // approximate (HyperLogLog); exact for a set this small
        db.SetCombineLength(SetOperation.Union, [key1, key2], approximate: true).Should().Be(6);
        (await db.SetCombineLengthAsync(SetOperation.Union, [key1, key2], 4, approximate: true)).Should().Be(4);

        // Missing keys contribute nothing
        var key3 = Me() + "3";
        db.KeyDelete(key3, CommandFlags.FireAndForget);
        db.SetCombineLength(SetOperation.Union, [key1, key3]).Should().Be(5);
        (await db.SetCombineLengthAsync(SetOperation.Union, [key3])).Should().Be(0);
    }

    [Fact]
    public async Task set_combine_length_difference()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);

        var db = conn.GetDatabase();

        var key1 = Me() + "1";
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        db.SetAdd(key1, [0, 1, 2, 3, 4], CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        db.SetAdd(key2, [3, 4, 5], CommandFlags.FireAndForget);

        // difference (key1 - key2) = {0,1,2}
        db.SetCombineLength(SetOperation.Difference, [key1, key2]).Should().Be(3);
        (await db.SetCombineLengthAsync(SetOperation.Difference, [key1, key2])).Should().Be(3);
        // with limit
        db.SetCombineLength(SetOperation.Difference, [key1, key2], 2).Should().Be(2);
        (await db.SetCombineLengthAsync(SetOperation.Difference, [key1, key2], 2)).Should().Be(2);

        // difference against a missing key leaves the first set intact
        var key3 = Me() + "3";
        db.KeyDelete(key3, CommandFlags.FireAndForget);
        db.SetCombineLength(SetOperation.Difference, [key1, key3]).Should().Be(5);
        (await db.SetCombineLengthAsync(SetOperation.Difference, [key3, key1])).Should().Be(0);
    }

    [Fact]
    public async Task set_combine_length_intersect()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);

        var db = conn.GetDatabase();

        var key1 = Me() + "1";
        db.KeyDelete(key1, CommandFlags.FireAndForget);
        db.SetAdd(key1, [0, 1, 2, 3, 4], CommandFlags.FireAndForget);
        var key2 = Me() + "2";
        db.KeyDelete(key2, CommandFlags.FireAndForget);
        db.SetAdd(key2, [3, 4, 5], CommandFlags.FireAndForget);

        // intersect = {3,4}; SetCombineLength(Intersect) maps to SINTERCARD
        db.SetCombineLength(SetOperation.Intersect, [key1, key2]).Should().Be(2);
        (await db.SetCombineLengthAsync(SetOperation.Intersect, [key1, key2], 1)).Should().Be(1);
    }

    [Fact]
    public async Task s_scan()
    {
        await using var conn = Create();

        var server = GetAnyPrimary(conn);

        var key = Me();
        var db = conn.GetDatabase();
        int totalUnfiltered = 0, totalFiltered = 0;
        for (int i = 1; i < 1001; i++)
        {
            db.SetAdd(key, i, CommandFlags.FireAndForget);
            totalUnfiltered += i;
            if (i.ToString().Contains('3')) totalFiltered += i;
        }

        var unfilteredActual = db.SetScan(key).Select(x => (int)x).Sum();
        unfilteredActual.Should().Be(totalUnfiltered);
        if (server.Features.Scan)
        {
            var filteredActual = db.SetScan(key, "*3*").Select(x => (int)x).Sum();
            filteredActual.Should().Be(totalFiltered);
        }
    }

    [Fact]
    public async Task set_remove_arg_tests()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();

        RedisValue[]? values = null;
        Assert.Throws<ArgumentNullException>(() => db.SetRemove(key, values!));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await db.SetRemoveAsync(key, values!).ForAwait()).ForAwait();

        values = [];
        db.SetRemove(key, values).Should().Be(0);
        (await db.SetRemoveAsync(key, values).ForAwait()).Should().Be(0);
    }

    [Fact]
    public async Task set_pop_multi_multi()
    {
        await using var conn = Create(require: RedisFeatures.v3_2_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        for (int i = 1; i < 11; i++)
        {
            _ = db.SetAddAsync(key, i, CommandFlags.FireAndForget);
        }

        var random = db.SetPop(key);
        random.IsNull.Should().BeFalse();
        ((int)random > 0).Should().BeTrue();
        ((int)random <= 10).Should().BeTrue();
        db.SetLength(key).Should().Be(9);

        var moreRandoms = db.SetPop(key, 2);
        moreRandoms.Length.Should().Be(2);
        moreRandoms[0].IsNull.Should().BeFalse();
        db.SetLength(key).Should().Be(7);
    }

    [Fact]
    public async Task set_pop_multi_single()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        for (int i = 1; i < 11; i++)
        {
            db.SetAdd(key, i, CommandFlags.FireAndForget);
        }

        var random = db.SetPop(key);
        random.IsNull.Should().BeFalse();
        ((int)random > 0).Should().BeTrue();
        ((int)random <= 10).Should().BeTrue();
        db.SetLength(key).Should().Be(9);

        var moreRandoms = db.SetPop(key, 1);
        moreRandoms.Should().ContainSingle();
        moreRandoms[0].IsNull.Should().BeFalse();
        db.SetLength(key).Should().Be(8);
    }

    [Fact]
    public async Task set_pop_multi_multi_async()
    {
        await using var conn = Create(require: RedisFeatures.v3_2_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        for (int i = 1; i < 11; i++)
        {
            db.SetAdd(key, i, CommandFlags.FireAndForget);
        }

        var random = await db.SetPopAsync(key).ForAwait();
        random.IsNull.Should().BeFalse();
        ((int)random > 0).Should().BeTrue();
        ((int)random <= 10).Should().BeTrue();
        db.SetLength(key).Should().Be(9);

        var moreRandoms = await db.SetPopAsync(key, 2).ForAwait();
        moreRandoms.Length.Should().Be(2);
        moreRandoms[0].IsNull.Should().BeFalse();
        db.SetLength(key).Should().Be(7);
    }

    [Fact]
    public async Task set_pop_multi_single_async()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        for (int i = 1; i < 11; i++)
        {
            db.SetAdd(key, i, CommandFlags.FireAndForget);
        }

        var random = await db.SetPopAsync(key).ForAwait();
        random.IsNull.Should().BeFalse();
        ((int)random > 0).Should().BeTrue();
        ((int)random <= 10).Should().BeTrue();
        db.SetLength(key).Should().Be(9);

        var moreRandoms = db.SetPop(key, 1);
        moreRandoms.Should().ContainSingle();
        moreRandoms[0].IsNull.Should().BeFalse();
        db.SetLength(key).Should().Be(8);
    }

    [Fact]
    public async Task set_pop_multi_zero_async()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        for (int i = 1; i < 11; i++)
        {
            db.SetAdd(key, i, CommandFlags.FireAndForget);
        }

        var t = db.SetPopAsync(key, count: 0);
        t.IsCompleted.Should().BeTrue(); // sync
        var arr = await t;
        arr.Should().BeEmpty();

        db.SetLength(key).Should().Be(10);
    }

    [Fact]
    public async Task set_add_zero()
    {
        //Arrange
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);

        //Act
        var result = db.SetAdd(key, Array.Empty<RedisValue>());

        //Assert
        result.Should().Be(0);

        db.SetLength(key).Should().Be(0);
    }

    [Fact]
    public async Task set_add_zero_async()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);

        var t = db.SetAddAsync(key, Array.Empty<RedisValue>());
        t.IsCompleted.Should().BeTrue(); // sync
        var count = await t;
        count.Should().Be(0);

        db.SetLength(key).Should().Be(0);
    }

    [Fact]
    public async Task set_pop_multi_nil()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v3_2_0);

        var db = conn.GetDatabase();
        var key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);

        //Act
        var arr = db.SetPop(key, 1);

        //Assert
        arr.Should().BeEmpty();
    }

    [Fact]
    public async Task test_sort_readonly_primary()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();
        await db.KeyDeleteAsync(key);

        var random = new Random();
        var items = Enumerable.Repeat(0, 200).Select(_ => random.Next()).ToList();
        await db.SetAddAsync(key, items.Select(x => (RedisValue)x).ToArray());
        items.Sort();

        var result = db.Sort(key).Select(x => (int)x);
        result.Should().Equal(items);

        result = (await db.SortAsync(key)).Select(x => (int)x);
        result.Should().Equal(items);
    }

    [Fact]
    public async Task test_sort_readonly_replica()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        await db.KeyDeleteAsync(key);

        var random = new Random();
        var items = Enumerable.Repeat(0, 200).Select(_ => random.Next()).ToList();
        await db.SetAddAsync(key, items.Select(x => (RedisValue)x).ToArray());

        await using var readonlyConn = Create(configuration: TestConfig.Current.ReplicaServerAndPort, require: RedisFeatures.v7_0_0_rc1);
        var readonlyDb = conn.GetDatabase();

        items.Sort();

        var result = readonlyDb.Sort(key).Select(x => (int)x);
        result.Should().Equal(items);

        result = (await readonlyDb.SortAsync(key)).Select(x => (int)x);
        result.Should().Equal(items);
    }
}

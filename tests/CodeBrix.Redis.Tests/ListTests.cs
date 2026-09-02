using System;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class ListTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task ranges()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();

        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.ListRightPush(key, "abcdefghijklmnopqrstuvwxyz".Select(x => (RedisValue)x.ToString()).ToArray(), CommandFlags.FireAndForget);

        db.ListLength(key).Should().Be(26);
        string.Concat(db.ListRange(key)).Should().Be("abcdefghijklmnopqrstuvwxyz");

        var last10 = db.ListRange(key, -10, -1);
        string.Concat(last10).Should().Be("qrstuvwxyz");
        db.ListTrim(key, 0, -11, CommandFlags.FireAndForget);

        db.ListLength(key).Should().Be(16);
        string.Concat(db.ListRange(key)).Should().Be("abcdefghijklmnop");
    }

    [Fact]
    public async Task list_left_push_empty_values()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var result = db.ListLeftPush(key, Array.Empty<RedisValue>(), When.Always, CommandFlags.None);
        result.Should().Be(0);
    }

    [Fact]
    public async Task list_left_push_key_does_not_exists()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var result = db.ListLeftPush(key, ["testvalue"], When.Exists, CommandFlags.None);
        result.Should().Be(0);
    }

    [Fact]
    public async Task list_left_push_to_exisiting_key()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var pushResult = db.ListLeftPush(key, ["testvalue1"], CommandFlags.None);
        pushResult.Should().Be(1);
        var pushXResult = db.ListLeftPush(key, ["testvalue2"], When.Exists, CommandFlags.None);
        pushXResult.Should().Be(2);

        var rangeResult = db.ListRange(key, 0, -1);
        rangeResult.Length.Should().Be(2);
        rangeResult[0].Should().Be("testvalue2");
        rangeResult[1].Should().Be("testvalue1");
    }

    [Fact]
    public async Task list_left_push_multiple_to_exisiting_key()
    {
        await using var conn = Create(require: RedisFeatures.v4_0_0);

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var pushResult = db.ListLeftPush(key, ["testvalue1"], CommandFlags.None);
        pushResult.Should().Be(1);
        var pushXResult = db.ListLeftPush(key, ["testvalue2", "testvalue3"], When.Exists, CommandFlags.None);
        pushXResult.Should().Be(3);

        var rangeResult = db.ListRange(key, 0, -1);
        rangeResult.Length.Should().Be(3);
        rangeResult[0].Should().Be("testvalue3");
        rangeResult[1].Should().Be("testvalue2");
        rangeResult[2].Should().Be("testvalue1");
    }

    [Fact]
    public async Task list_left_push_async_empty_values()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var result = await db.ListLeftPushAsync(key, Array.Empty<RedisValue>(), When.Always, CommandFlags.None);
        result.Should().Be(0);
    }

    [Fact]
    public async Task list_left_push_async_key_does_not_exists()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var result = await db.ListLeftPushAsync(key, ["testvalue"], When.Exists, CommandFlags.None);
        result.Should().Be(0);
    }

    [Fact]
    public async Task list_left_push_async_to_exisiting_key()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var pushResult = await db.ListLeftPushAsync(key, ["testvalue1"], CommandFlags.None);
        pushResult.Should().Be(1);
        var pushXResult = await db.ListLeftPushAsync(key, ["testvalue2"], When.Exists, CommandFlags.None);
        pushXResult.Should().Be(2);

        var rangeResult = db.ListRange(key, 0, -1);
        rangeResult.Length.Should().Be(2);
        rangeResult[0].Should().Be("testvalue2");
        rangeResult[1].Should().Be("testvalue1");
    }

    [Fact]
    public async Task list_left_push_async_multiple_to_exisiting_key()
    {
        await using var conn = Create(require: RedisFeatures.v4_0_0);

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var pushResult = await db.ListLeftPushAsync(key, ["testvalue1"], CommandFlags.None);
        pushResult.Should().Be(1);
        var pushXResult = await db.ListLeftPushAsync(key, ["testvalue2", "testvalue3"], When.Exists, CommandFlags.None);
        pushXResult.Should().Be(3);

        var rangeResult = db.ListRange(key, 0, -1);
        rangeResult.Length.Should().Be(3);
        rangeResult[0].Should().Be("testvalue3");
        rangeResult[1].Should().Be("testvalue2");
        rangeResult[2].Should().Be("testvalue1");
    }

    [Fact]
    public async Task list_right_push_empty_values()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var result = db.ListRightPush(key, Array.Empty<RedisValue>(), When.Always, CommandFlags.None);
        result.Should().Be(0);
    }

    [Fact]
    public async Task list_right_push_key_does_not_exists()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var result = db.ListRightPush(key, ["testvalue"], When.Exists, CommandFlags.None);
        result.Should().Be(0);
    }

    [Fact]
    public async Task list_right_push_to_exisiting_key()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var pushResult = db.ListRightPush(key, ["testvalue1"], CommandFlags.None);
        pushResult.Should().Be(1);
        var pushXResult = db.ListRightPush(key, ["testvalue2"], When.Exists, CommandFlags.None);
        pushXResult.Should().Be(2);

        var rangeResult = db.ListRange(key, 0, -1);
        rangeResult.Length.Should().Be(2);
        rangeResult[0].Should().Be("testvalue1");
        rangeResult[1].Should().Be("testvalue2");
    }

    [Fact]
    public async Task list_right_push_multiple_to_exisiting_key()
    {
        await using var conn = Create(require: RedisFeatures.v4_0_0);

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var pushResult = db.ListRightPush(key, ["testvalue1"], CommandFlags.None);
        pushResult.Should().Be(1);
        var pushXResult = db.ListRightPush(key, ["testvalue2", "testvalue3"], When.Exists, CommandFlags.None);
        pushXResult.Should().Be(3);

        var rangeResult = db.ListRange(key, 0, -1);
        rangeResult.Length.Should().Be(3);
        rangeResult[0].Should().Be("testvalue1");
        rangeResult[1].Should().Be("testvalue2");
        rangeResult[2].Should().Be("testvalue3");
    }

    [Fact]
    public async Task list_right_push_async_empty_values()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var result = await db.ListRightPushAsync(key, Array.Empty<RedisValue>(), When.Always, CommandFlags.None);
        result.Should().Be(0);
    }

    [Fact]
    public async Task list_right_push_async_key_does_not_exists()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        var result = await db.ListRightPushAsync(key, ["testvalue"], When.Exists, CommandFlags.None);
        result.Should().Be(0);
    }

    [Fact]
    public async Task list_right_push_async_to_exisiting_key()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var pushResult = await db.ListRightPushAsync(key, ["testvalue1"], CommandFlags.None);
        pushResult.Should().Be(1);
        var pushXResult = await db.ListRightPushAsync(key, ["testvalue2"], When.Exists, CommandFlags.None);
        pushXResult.Should().Be(2);

        var rangeResult = db.ListRange(key, 0, -1);
        rangeResult.Length.Should().Be(2);
        rangeResult[0].Should().Be("testvalue1");
        rangeResult[1].Should().Be("testvalue2");
    }

    [Fact]
    public async Task list_right_push_async_multiple_to_exisiting_key()
    {
        await using var conn = Create(require: RedisFeatures.v4_0_0);

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        var pushResult = await db.ListRightPushAsync(key, ["testvalue1"], CommandFlags.None);
        pushResult.Should().Be(1);
        var pushXResult = await db.ListRightPushAsync(key, ["testvalue2", "testvalue3"], When.Exists, CommandFlags.None);
        pushXResult.Should().Be(3);

        var rangeResult = db.ListRange(key, 0, -1);
        rangeResult.Length.Should().Be(3);
        rangeResult[0].Should().Be("testvalue1");
        rangeResult[1].Should().Be("testvalue2");
        rangeResult[2].Should().Be("testvalue3");
    }

    [Fact]
    public async Task list_move()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        RedisKey src = Me();
        RedisKey dest = Me() + "dest";
        db.KeyDelete(src, CommandFlags.FireAndForget);

        var pushResult = await db.ListRightPushAsync(src, ["testvalue1", "testvalue2"]);
        pushResult.Should().Be(2);

        var rangeResult1 = db.ListMove(src, dest, ListSide.Left, ListSide.Right);
        var rangeResult2 = db.ListMove(src, dest, ListSide.Left, ListSide.Left);
        var rangeResult3 = db.ListMove(dest, src, ListSide.Right, ListSide.Right);
        var rangeResult4 = db.ListMove(dest, src, ListSide.Right, ListSide.Left);
        rangeResult1.Should().Be("testvalue1");
        rangeResult2.Should().Be("testvalue2");
        rangeResult3.Should().Be("testvalue1");
        rangeResult4.Should().Be("testvalue2");
    }

    [Fact]
    public async Task list_move_key_does_not_exist()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        RedisKey src = Me();
        RedisKey dest = Me() + "dest";
        db.KeyDelete(src, CommandFlags.FireAndForget);

        var rangeResult1 = db.ListMove(src, dest, ListSide.Left, ListSide.Right);
        rangeResult1.IsNull.Should().BeTrue();
    }

    [Fact]
    public async Task list_move_multiple_up_to()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);

        var db = conn.GetDatabase();
        RedisKey src = Me();
        RedisKey dest = Me() + "dest";
        db.KeyDelete([src, dest], CommandFlags.FireAndForget);

        await db.ListRightPushAsync(src, ["a", "b", "c", "d"]);

        // move up-to 2 from the head of src onto the tail of dest
        var moved = await db.ListMoveAsync(src, dest, ListSide.Left, ListSide.Right, 2);
        Assert.NotNull(moved);
        moved.Select(x => x.ToString()).Should().Equal(["a", "b"]);
        db.ListRange(src).Select(x => x.ToString()).Should().Equal(["c", "d"]);
        db.ListRange(dest).Select(x => x.ToString()).Should().Equal(["a", "b"]);

        // COUNT tolerates asking for more than exist: moves what's left (here, the final 2)
        var rest = db.ListMove(src, dest, ListSide.Left, ListSide.Right, 100);
        Assert.NotNull(rest);
        rest.Select(x => x.ToString()).Should().Equal(["c", "d"]);
        db.ListLength(src).Should().Be(0);

        // COUNT against an empty source moves nothing and returns null (as LMOVE does)
        var none = db.ListMove(src, dest, ListSide.Left, ListSide.Right, 5);
        none.Should().BeNull();
    }

    [Fact]
    public async Task list_move_multiple_ordering()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);

        var db = conn.GetDatabase();
        RedisKey src = Me();
        RedisKey dest = Me() + "dest";

        // BULK preserves the source's relative order...
        db.KeyDelete([src, dest], CommandFlags.FireAndForget);
        await db.ListRightPushAsync(src, ["a", "b", "c", "d"]);
        var bulk = db.ListMove(src, dest, ListSide.Left, ListSide.Left, 2, ListMoveCount.UpTo, ListMoveOrder.Bulk);
        bulk!.Select(x => x.ToString()).Should().Equal(["a", "b"]);

        // ...whereas OBO moves them one-by-one, reversing them when pushed onto the head.
        db.KeyDelete([src, dest], CommandFlags.FireAndForget);
        await db.ListRightPushAsync(src, ["a", "b", "c", "d"]);
        var obo = db.ListMove(src, dest, ListSide.Left, ListSide.Left, 2, ListMoveCount.UpTo, ListMoveOrder.OneByOne);
        obo!.Select(x => x.ToString()).Should().Equal(["b", "a"]);
    }

    [Fact]
    public async Task list_move_multiple_exactly()
    {
        await using var conn = Create(require: RedisFeatures.v8_10_0);

        var db = conn.GetDatabase();
        RedisKey src = Me();
        RedisKey dest = Me() + "dest";
        db.KeyDelete([src, dest], CommandFlags.FireAndForget);

        await db.ListRightPushAsync(src, ["a", "b"]);

        // EXACTLY is all-or-nothing: too few elements => null, and the source is left untouched
        var unsatisfied = db.ListMove(src, dest, ListSide.Left, ListSide.Right, 3, ListMoveCount.Exactly);
        unsatisfied.Should().BeNull();
        db.ListRange(src).Select(x => x.ToString()).Should().Equal(["a", "b"]);
        db.ListLength(dest).Should().Be(0);

        // exactly the right number => all move
        var satisfied = db.ListMove(src, dest, ListSide.Left, ListSide.Right, 2, ListMoveCount.Exactly);
        Assert.NotNull(satisfied);
        satisfied.Select(x => x.ToString()).Should().Equal(["a", "b"]);
        db.ListLength(src).Should().Be(0);
    }

    [Fact]
    public async Task list_position_happy_path()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string val = "foo";
        db.KeyDelete(key);

        db.ListLeftPush(key, val);
        var res = db.ListPosition(key, val);

        res.Should().Be(0);
    }

    [Fact]
    public async Task list_position_empty()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string val = "foo";
        db.KeyDelete(key);

        var res = db.ListPosition(key, val);

        res.Should().Be(-1);
    }

    [Fact]
    public async Task list_positions_happy_path()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        db.KeyDelete(key);

        for (var i = 0; i < 10; i++)
        {
            db.ListLeftPush(key, foo);
            db.ListLeftPush(key, bar);
            db.ListLeftPush(key, baz);
        }

        var res = db.ListPositions(key, foo, 5);

        foreach (var item in res)
        {
            (item % 3).Should().Be(2);
        }

        res.Length.Should().Be(5);
    }

    [Fact]
    public async Task list_positions_too_few()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        db.KeyDelete(key);

        for (var i = 0; i < 10; i++)
        {
            db.ListLeftPush(key, bar);
            db.ListLeftPush(key, baz);
        }

        db.ListLeftPush(key, foo);

        var res = db.ListPositions(key, foo, 5);
        res.Should().ContainSingle();
        res.Single().Should().Be(0);
    }

    [Fact]
    public async Task list_positions_all()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        db.KeyDelete(key);

        for (var i = 0; i < 10; i++)
        {
            db.ListLeftPush(key, foo);
            db.ListLeftPush(key, bar);
            db.ListLeftPush(key, baz);
        }

        var res = db.ListPositions(key, foo, 0);

        foreach (var item in res)
        {
            (item % 3).Should().Be(2);
        }

        res.Length.Should().Be(10);
    }

    [Fact]
    public async Task list_positions_all_limit_length()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        db.KeyDelete(key);

        for (var i = 0; i < 10; i++)
        {
            db.ListLeftPush(key, foo);
            db.ListLeftPush(key, bar);
            db.ListLeftPush(key, baz);
        }

        var res = db.ListPositions(key, foo, 0, maxLength: 15);

        foreach (var item in res)
        {
            (item % 3).Should().Be(2);
        }

        res.Length.Should().Be(5);
    }

    [Fact]
    public async Task list_positions_empty()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        db.KeyDelete(key);

        for (var i = 0; i < 10; i++)
        {
            db.ListLeftPush(key, bar);
            db.ListLeftPush(key, baz);
        }

        var res = db.ListPositions(key, foo, 5);

        res.Should().BeEmpty();
    }

    [Fact]
    public async Task list_position_by_rank()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        db.KeyDelete(key);

        for (var i = 0; i < 10; i++)
        {
            db.ListLeftPush(key, foo);
            db.ListLeftPush(key, bar);
            db.ListLeftPush(key, baz);
        }

        const int rank = 6;

        var res = db.ListPosition(key, foo, rank: rank);

        res.Should().Be((3 * rank) - 1);
    }

    [Fact]
    public async Task list_position_limit_so_null()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        db.KeyDelete(key);

        for (var i = 0; i < 10; i++)
        {
            db.ListLeftPush(key, bar);
            db.ListLeftPush(key, baz);
        }

        db.ListRightPush(key, foo);

        var res = db.ListPosition(key, foo, maxLength: 20);

        res.Should().Be(-1);
    }

    [Fact]
    public async Task list_position_happy_path_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string val = "foo";
        await db.KeyDeleteAsync(key);

        await db.ListLeftPushAsync(key, val);
        var res = await db.ListPositionAsync(key, val);

        res.Should().Be(0);
    }

    [Fact]
    public async Task list_position_empty_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string val = "foo";
        await db.KeyDeleteAsync(key);

        var res = await db.ListPositionAsync(key, val);

        res.Should().Be(-1);
    }

    [Fact]
    public async Task list_positions_happy_path_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        await db.KeyDeleteAsync(key);

        for (var i = 0; i < 10; i++)
        {
            await db.ListLeftPushAsync(key, foo);
            await db.ListLeftPushAsync(key, bar);
            await db.ListLeftPushAsync(key, baz);
        }

        var res = await db.ListPositionsAsync(key, foo, 5);

        foreach (var item in res)
        {
            (item % 3).Should().Be(2);
        }

        res.Length.Should().Be(5);
    }

    [Fact]
    public async Task list_positions_too_few_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        await db.KeyDeleteAsync(key);

        for (var i = 0; i < 10; i++)
        {
            await db.ListLeftPushAsync(key, bar);
            await db.ListLeftPushAsync(key, baz);
        }

        db.ListLeftPush(key, foo);

        var res = await db.ListPositionsAsync(key, foo, 5);
        res.Should().ContainSingle();
        res.Single().Should().Be(0);
    }

    [Fact]
    public async Task list_positions_all_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        await db.KeyDeleteAsync(key);

        for (var i = 0; i < 10; i++)
        {
            await db.ListLeftPushAsync(key, foo);
            await db.ListLeftPushAsync(key, bar);
            await db.ListLeftPushAsync(key, baz);
        }

        var res = await db.ListPositionsAsync(key, foo, 0);

        foreach (var item in res)
        {
            (item % 3).Should().Be(2);
        }

        res.Length.Should().Be(10);
    }

    [Fact]
    public async Task list_positions_all_limit_length_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        await db.KeyDeleteAsync(key);

        for (var i = 0; i < 10; i++)
        {
            await db.ListLeftPushAsync(key, foo);
            await db.ListLeftPushAsync(key, bar);
            await db.ListLeftPushAsync(key, baz);
        }

        var res = await db.ListPositionsAsync(key, foo, 0, maxLength: 15);

        foreach (var item in res)
        {
            (item % 3).Should().Be(2);
        }

        res.Length.Should().Be(5);
    }

    [Fact]
    public async Task list_positions_empty_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        await db.KeyDeleteAsync(key);

        for (var i = 0; i < 10; i++)
        {
            await db.ListLeftPushAsync(key, bar);
            await db.ListLeftPushAsync(key, baz);
        }

        var res = await db.ListPositionsAsync(key, foo, 5);

        res.Should().BeEmpty();
    }

    [Fact]
    public async Task list_position_by_rank_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        await db.KeyDeleteAsync(key);

        for (var i = 0; i < 10; i++)
        {
            await db.ListLeftPushAsync(key, foo);
            await db.ListLeftPushAsync(key, bar);
            await db.ListLeftPushAsync(key, baz);
        }

        const int rank = 6;

        var res = await db.ListPositionAsync(key, foo, rank: rank);

        res.Should().Be((3 * rank) - 1);
    }

    [Fact]
    public async Task list_position_limit_so_null_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        await db.KeyDeleteAsync(key);

        for (var i = 0; i < 10; i++)
        {
            await db.ListLeftPushAsync(key, bar);
            await db.ListLeftPushAsync(key, baz);
        }

        await db.ListRightPushAsync(key, foo);

        var res = await db.ListPositionAsync(key, foo, maxLength: 20);

        res.Should().Be(-1);
    }

    [Fact]
    public async Task list_position_fire_and_forget_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        await db.KeyDeleteAsync(key);

        for (var i = 0; i < 10; i++)
        {
            await db.ListLeftPushAsync(key, foo);
            await db.ListLeftPushAsync(key, bar);
            await db.ListLeftPushAsync(key, baz);
        }

        await db.ListRightPushAsync(key, foo);

        var res = await db.ListPositionAsync(key, foo, maxLength: 20, flags: CommandFlags.FireAndForget);

        res.Should().Be(-1);
    }

    [Fact]
    public async Task list_position_fire_and_forget()
    {
        await using var conn = Create(require: RedisFeatures.v6_0_6);

        var db = conn.GetDatabase();
        var key = Me();
        const string foo = "foo",
                     bar = "bar",
                     baz = "baz";

        db.KeyDelete(key);

        for (var i = 0; i < 10; i++)
        {
            db.ListLeftPush(key, foo);
            db.ListLeftPush(key, bar);
            db.ListLeftPush(key, baz);
        }

        db.ListRightPush(key, foo);

        var res = db.ListPosition(key, foo, maxLength: 20, flags: CommandFlags.FireAndForget);

        res.Should().Be(-1);
    }

    [Fact]
    public async Task list_multi_pop_single_key_async()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        await db.KeyDeleteAsync(key);

        db.ListLeftPush(key, "yankees");
        db.ListLeftPush(key, "blue jays");
        db.ListLeftPush(key, "orioles");
        db.ListLeftPush(key, "red sox");
        db.ListLeftPush(key, "rays");

        var res = await db.ListLeftPopAsync([key], 1);

        res.IsNull.Should().BeFalse();
        res.Values.Should().ContainSingle();
        res.Values[0].Should().Be("rays");

        res = await db.ListRightPopAsync([key], 2);

        res.IsNull.Should().BeFalse();
        res.Values.Length.Should().Be(2);
        res.Values[0].Should().Be("yankees");
        res.Values[1].Should().Be("blue jays");
    }

    [Fact]
    public async Task list_multi_pop_multiple_keys_async()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        await db.KeyDeleteAsync(key);

        db.ListLeftPush(key, "yankees");
        db.ListLeftPush(key, "blue jays");
        db.ListLeftPush(key, "orioles");
        db.ListLeftPush(key, "red sox");
        db.ListLeftPush(key, "rays");

        var res = await db.ListLeftPopAsync(["empty-key", key, "also-empty"], 2);

        res.IsNull.Should().BeFalse();
        res.Values.Length.Should().Be(2);
        res.Values[0].Should().Be("rays");
        res.Values[1].Should().Be("red sox");

        res = await db.ListRightPopAsync(["empty-key", key, "also-empty"], 1);

        res.IsNull.Should().BeFalse();
        res.Values.Should().ContainSingle();
        res.Values[0].Should().Be("yankees");
    }

    [Fact]
    public async Task list_multi_pop_single_key()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key);

        db.ListLeftPush(key, "yankees");
        db.ListLeftPush(key, "blue jays");
        db.ListLeftPush(key, "orioles");
        db.ListLeftPush(key, "red sox");
        db.ListLeftPush(key, "rays");

        var res = db.ListLeftPop([key], 1);

        res.IsNull.Should().BeFalse();
        res.Values.Should().ContainSingle();
        res.Values[0].Should().Be("rays");

        res = db.ListRightPop([key], 2);

        res.IsNull.Should().BeFalse();
        res.Values.Length.Should().Be(2);
        res.Values[0].Should().Be("yankees");
        res.Values[1].Should().Be("blue jays");
    }

    [Fact]
    public async Task list_multi_pop_zero_count()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key);

        var exception = await Assert.ThrowsAsync<RedisServerException>(() => db.ListLeftPopAsync([key], 0));
        exception.Message.Should().Contain("ERR count should be greater than 0");
    }

    [Fact]
    public async Task list_multi_pop_empty()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        db.KeyDelete(key);

        var res = await db.ListLeftPopAsync([key], 1);
        res.IsNull.Should().BeTrue();
    }

    [Fact]
    public async Task list_multi_pop_empty_keys()
    {
        await using var conn = Create(require: RedisFeatures.v7_0_0_rc1);

        var db = conn.GetDatabase();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => db.ListRightPop(Array.Empty<RedisKey>(), 5));
        exception.Message.Should().Contain("keys must have a size of at least 1");

        exception = Assert.Throws<ArgumentOutOfRangeException>(() => db.ListLeftPop(Array.Empty<RedisKey>(), 5));
        exception.Message.Should().Contain("keys must have a size of at least 1");
    }
}

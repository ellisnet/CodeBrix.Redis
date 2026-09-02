using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// Tests for <see href="https://redis.io/commands#hash"/>.
/// </summary>
[RunPerProtocol]
public class HashTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task test_incr_by()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();
        _ = db.KeyDeleteAsync(key).ForAwait();

        const int iterations = 100;
        var aTasks = new Task<long>[iterations];
        var bTasks = new Task<long>[iterations];
        for (int i = 1; i < iterations + 1; i++)
        {
            aTasks[i - 1] = db.HashIncrementAsync(key, "a", 1);
            bTasks[i - 1] = db.HashIncrementAsync(key, "b", -1);
        }
        await Task.WhenAll(bTasks).ForAwait();
        for (int i = 1; i < iterations + 1; i++)
        {
            aTasks[i - 1].Result.Should().Be(i);
            bTasks[i - 1].Result.Should().Be(-i);
        }
    }

    [Fact]
    public async Task scan_async()
    {
        await using var conn = Create(require: RedisFeatures.v2_8_0);

        var db = conn.GetDatabase();
        var key = Me();
        await db.KeyDeleteAsync(key);
        for (int i = 0; i < 200; i++)
        {
            await db.HashSetAsync(key, "key" + i, "value " + i);
        }

        int count = 0;
        // works for async
        await foreach (var _ in db.HashScanAsync(key, pageSize: 20))
        {
            count++;
        }
        count.Should().Be(200);

        // and sync=>async (via cast)
        count = 0;
        await foreach (var _ in (IAsyncEnumerable<HashEntry>)db.HashScan(key, pageSize: 20))
        {
            count++;
        }
        count.Should().Be(200);

        // and sync (native)
        count = 0;
        foreach (var _ in db.HashScan(key, pageSize: 20))
        {
            count++;
        }
        count.Should().Be(200);

        // and async=>sync (via cast)
        count = 0;
        foreach (var _ in (IEnumerable<HashEntry>)db.HashScanAsync(key, pageSize: 20))
        {
            count++;
        }
        count.Should().Be(200);
    }

    [Fact]
    public async Task scan()
    {
        await using var conn = Create(require: RedisFeatures.v2_8_0);

        var db = conn.GetDatabase();

        var key = Me();
        _ = db.KeyDeleteAsync(key);
        _ = db.HashSetAsync(key, "abc", "def");
        _ = db.HashSetAsync(key, "ghi", "jkl");
        _ = db.HashSetAsync(key, "mno", "pqr");

        var t1 = db.HashScan(key);
        var t2 = db.HashScan(key, "*h*");
        var t3 = db.HashScan(key);
        var t4 = db.HashScan(key, "*h*");

        var v1 = t1.ToArray();
        var v2 = t2.ToArray();
        var v3 = t3.ToArray();
        var v4 = t4.ToArray();

        v1.Length.Should().Be(3);
        v2.Should().ContainSingle();
        v3.Length.Should().Be(3);
        v4.Should().ContainSingle();
        Array.Sort(v1, (x, y) => string.Compare(x.Name, y.Name));
        Array.Sort(v2, (x, y) => string.Compare(x.Name, y.Name));
        Array.Sort(v3, (x, y) => string.Compare(x.Name, y.Name));
        Array.Sort(v4, (x, y) => string.Compare(x.Name, y.Name));

        string.Join(",", v1.Select(pair => pair.Name + "=" + pair.Value)).Should().Be("abc=def,ghi=jkl,mno=pqr");
        string.Join(",", v2.Select(pair => pair.Name + "=" + pair.Value)).Should().Be("ghi=jkl");
        string.Join(",", v3.Select(pair => pair.Name + "=" + pair.Value)).Should().Be("abc=def,ghi=jkl,mno=pqr");
        string.Join(",", v4.Select(pair => pair.Name + "=" + pair.Value)).Should().Be("ghi=jkl");
    }

    [Fact]
    public async Task scan_no_values_async()
    {
        await using var conn = Create(require: RedisFeatures.v7_4_0_rc1);

        var db = conn.GetDatabase();
        var key = Me();
        await db.KeyDeleteAsync(key);
        for (int i = 0; i < 200; i++)
        {
            await db.HashSetAsync(key, "key" + i, "value " + i);
        }

        int count = 0;
        // works for async
        await foreach (var _ in db.HashScanNoValuesAsync(key, pageSize: 20))
        {
            count++;
        }
        count.Should().Be(200);

        // and sync=>async (via cast)
        count = 0;
        await foreach (var _ in (IAsyncEnumerable<RedisValue>)db.HashScanNoValues(key, pageSize: 20))
        {
            count++;
        }
        count.Should().Be(200);

        // and sync (native)
        count = 0;
        foreach (var _ in db.HashScanNoValues(key, pageSize: 20))
        {
            count++;
        }
        count.Should().Be(200);

        // and async=>sync (via cast)
        count = 0;
        foreach (var _ in (IEnumerable<RedisValue>)db.HashScanNoValuesAsync(key, pageSize: 20))
        {
            count++;
        }
        count.Should().Be(200);
    }

    [Fact]
    public async Task scan_no_values()
    {
        await using var conn = Create(require: RedisFeatures.v7_4_0_rc1);

        var db = conn.GetDatabase();

        var key = Me();
        _ = db.KeyDeleteAsync(key);
        _ = db.HashSetAsync(key, "abc", "def");
        _ = db.HashSetAsync(key, "ghi", "jkl");
        _ = db.HashSetAsync(key, "mno", "pqr");

        var t1 = db.HashScanNoValues(key);
        var t2 = db.HashScanNoValues(key, "*h*");
        var t3 = db.HashScanNoValues(key);
        var t4 = db.HashScanNoValues(key, "*h*");

        var v1 = t1.ToArray();
        var v2 = t2.ToArray();
        var v3 = t3.ToArray();
        var v4 = t4.ToArray();

        v1.Length.Should().Be(3);
        v2.Should().ContainSingle();
        v3.Length.Should().Be(3);
        v4.Should().ContainSingle();

        Array.Sort(v1);
        Array.Sort(v2);
        Array.Sort(v3);
        Array.Sort(v4);

        v1.Should().Equal(new RedisValue[] { "abc", "ghi", "mno" });
        v2.Should().Equal(new RedisValue[] { "ghi" });
        v3.Should().Equal(new RedisValue[] { "abc", "ghi", "mno" });
        v4.Should().Equal(new RedisValue[] { "ghi" });
    }

    [Fact]
    public async Task test_increment_on_hash_that_doesnt_exist()
    {
        //Arrange
        await using var conn = Create();
        var db = conn.GetDatabase();
        _ = db.KeyDeleteAsync("keynotexist");
        var result1 = db.Wait(db.HashIncrementAsync("keynotexist", "fieldnotexist", 1));

        //Act
        var result2 = db.Wait(db.HashIncrementAsync("keynotexist", "anotherfieldnotexist", 1));

        //Assert
        result1.Should().Be(1);
        result2.Should().Be(1);
    }

    [Fact]
    public async Task test_incr_by_float()
    {
        await using var conn = Create(require: RedisFeatures.v2_6_0);

        var db = conn.GetDatabase();
        var key = Me();
        _ = db.KeyDeleteAsync(key).ForAwait();
        var aTasks = new Task<double>[1000];
        var bTasks = new Task<double>[1000];
        for (int i = 1; i < 1001; i++)
        {
            aTasks[i - 1] = db.HashIncrementAsync(key, "a", 1.0);
            bTasks[i - 1] = db.HashIncrementAsync(key, "b", -1.0);
        }
        await Task.WhenAll(bTasks).ForAwait();
        for (int i = 1; i < 1001; i++)
        {
            aTasks[i - 1].Result.Should().Be(i);
            bTasks[i - 1].Result.Should().Be(-i);
        }
    }

    [Fact]
    public async Task test_get_all()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var key = Me();
        await db.KeyDeleteAsync(key).ForAwait();
        var shouldMatch = new Dictionary<Guid, int>();
        var random = new Random();

        for (int i = 0; i < 1000; i++)
        {
            var guid = Guid.NewGuid();
            var value = random.Next(int.MaxValue);

            shouldMatch[guid] = value;

            _ = db.HashIncrementAsync(key, guid.ToString(), value);
        }

        var inRedis = (await db.HashGetAllAsync(key).ForAwait()).ToDictionary(
            x => Guid.Parse((string)x.Name!), x => int.Parse(x.Value!));

        inRedis.Count.Should().Be(shouldMatch.Count);

        foreach (var k in shouldMatch.Keys)
        {
            inRedis[k].Should().Be(shouldMatch[k]);
        }
    }

    [Fact]
    public async Task test_get()
    {
        await using var conn = Create();

        var key = Me();
        var db = conn.GetDatabase();
        var shouldMatch = new Dictionary<Guid, int>();
        var random = new Random();

        for (int i = 1; i < 1000; i++)
        {
            var guid = Guid.NewGuid();
            var value = random.Next(int.MaxValue);

            shouldMatch[guid] = value;

            _ = db.HashIncrementAsync(key, guid.ToString(), value);
        }

        foreach (var k in shouldMatch.Keys)
        {
            var inRedis = await db.HashGetAsync(key, k.ToString()).ForAwait();
            var num = int.Parse(inRedis!);

            num.Should().Be(shouldMatch[k]);
        }
    }

    /// <summary>
    /// Tests for <see href="https://redis.io/commands/hset"/>.
    /// </summary>
    [Fact]
    public async Task test_set()
    {
        //Arrange
        await using var conn = Create();
        var db = conn.GetDatabase();
        var hashkey = Me();
        var del = db.KeyDeleteAsync(hashkey).ForAwait();
        var val0 = db.HashGetAsync(hashkey, "field").ForAwait();
        var set0 = db.HashSetAsync(hashkey, "field", "value1").ForAwait();
        var val1 = db.HashGetAsync(hashkey, "field").ForAwait();
        var set1 = db.HashSetAsync(hashkey, "field", "value2").ForAwait();
        var val2 = db.HashGetAsync(hashkey, "field").ForAwait();
        var set2 = db.HashSetAsync(hashkey, "field-blob", Encoding.UTF8.GetBytes("value3")).ForAwait();
        var val3 = db.HashGetAsync(hashkey, "field-blob").ForAwait();
        var set3 = db.HashSetAsync(hashkey, "empty_type1", "").ForAwait();
        var val4 = db.HashGetAsync(hashkey, "empty_type1").ForAwait();
        var set4 = db.HashSetAsync(hashkey, "empty_type2", RedisValue.EmptyString).ForAwait();
        var val5 = db.HashGetAsync(hashkey, "empty_type2").ForAwait();

        //Act
        await del;

        //Assert
        ((string?)(await val0)).Should().BeNull();
        (await set0).Should().BeTrue();
        (await val1).Should().Be("value1");
        (await set1).Should().BeFalse();
        (await val2).Should().Be("value2");
        (await set2).Should().BeTrue();
        (await val3).Should().Be("value3");
        (await set3).Should().BeTrue();
        (await val4).Should().Be("");
        (await set4).Should().BeTrue();
        (await val5).Should().Be("");
    }

    /// <summary>
    /// Tests for <see href="https://redis.io/commands/hsetnx"/>.
    /// </summary>
    [Fact]
    public async Task test_set_not_exists()
    {
        //Arrange
        await using var conn = Create();
        var db = conn.GetDatabase();
        var hashkey = Me();
        var del = db.KeyDeleteAsync(hashkey).ForAwait();
        var val0 = db.HashGetAsync(hashkey, "field").ForAwait();
        var set0 = db.HashSetAsync(hashkey, "field", "value1", When.NotExists).ForAwait();
        var val1 = db.HashGetAsync(hashkey, "field").ForAwait();
        var set1 = db.HashSetAsync(hashkey, "field", "value2", When.NotExists).ForAwait();
        var val2 = db.HashGetAsync(hashkey, "field").ForAwait();
        var set2 = db.HashSetAsync(hashkey, "field-blob", Encoding.UTF8.GetBytes("value3"), When.NotExists).ForAwait();
        var val3 = db.HashGetAsync(hashkey, "field-blob").ForAwait();
        var set3 = db.HashSetAsync(hashkey, "field-blob", Encoding.UTF8.GetBytes("value3"), When.NotExists).ForAwait();

        //Act
        await del;

        //Assert
        ((string?)(await val0)).Should().BeNull();
        (await set0).Should().BeTrue();
        (await val1).Should().Be("value1");
        (await set1).Should().BeFalse();
        (await val2).Should().Be("value1");
        (await set2).Should().BeTrue();
        (await val3).Should().Be("value3");
        (await set3).Should().BeFalse();
    }

    /// <summary>
    /// Tests for <see href="https://redis.io/commands/hdel"/>.
    /// </summary>
    [Fact]
    public async Task test_del_single()
    {
        //Arrange
        await using var conn = Create();
        var db = conn.GetDatabase();
        var hashkey = Me();
        await db.KeyDeleteAsync(hashkey).ForAwait();
        var del0 = db.HashDeleteAsync(hashkey, "field").ForAwait();
        await db.HashSetAsync(hashkey, "field", "value").ForAwait();
        var del1 = db.HashDeleteAsync(hashkey, "field").ForAwait();

        //Act
        var del2 = db.HashDeleteAsync(hashkey, "field").ForAwait();

        //Assert
        (await del0).Should().BeFalse();
        (await del1).Should().BeTrue();
        (await del2).Should().BeFalse();
    }

    /// <summary>
    /// Tests for <see href="https://redis.io/commands/hdel"/>.
    /// </summary>
    [Fact]
    public async Task test_del_multi()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var hashkey = Me();
        db.HashSet(hashkey, "key1", "val1", flags: CommandFlags.FireAndForget);
        db.HashSet(hashkey, "key2", "val2", flags: CommandFlags.FireAndForget);
        db.HashSet(hashkey, "key3", "val3", flags: CommandFlags.FireAndForget);

        var s1 = db.HashExistsAsync(hashkey, "key1");
        var s2 = db.HashExistsAsync(hashkey, "key2");
        var s3 = db.HashExistsAsync(hashkey, "key3");

        var removed = db.HashDeleteAsync(hashkey, ["key1", "key3"]);

        var d1 = db.HashExistsAsync(hashkey, "key1");
        var d2 = db.HashExistsAsync(hashkey, "key2");
        var d3 = db.HashExistsAsync(hashkey, "key3");

        (await s1).Should().BeTrue();
        (await s2).Should().BeTrue();
        (await s3).Should().BeTrue();

        (await removed).Should().Be(2);

        (await d1).Should().BeFalse();
        (await d2).Should().BeTrue();
        (await d3).Should().BeFalse();

        var removeFinal = db.HashDeleteAsync(hashkey, ["key2"]);

        (await db.HashLengthAsync(hashkey).ForAwait()).Should().Be(0);
        (await removeFinal).Should().Be(1);
    }

    /// <summary>
    /// Tests for <see href="https://redis.io/commands/hdel"/>.
    /// </summary>
    [Fact]
    public async Task test_del_multi_inside_transaction()
    {
        await using var conn = Create();

        var tran = conn.GetDatabase().CreateTransaction();
        {
            var hashkey = Me();
            _ = tran.HashSetAsync(hashkey, "key1", "val1");
            _ = tran.HashSetAsync(hashkey, "key2", "val2");
            _ = tran.HashSetAsync(hashkey, "key3", "val3");

            var s1 = tran.HashExistsAsync(hashkey, "key1");
            var s2 = tran.HashExistsAsync(hashkey, "key2");
            var s3 = tran.HashExistsAsync(hashkey, "key3");

            var removed = tran.HashDeleteAsync(hashkey, ["key1", "key3"]);

            var d1 = tran.HashExistsAsync(hashkey, "key1");
            var d2 = tran.HashExistsAsync(hashkey, "key2");
            var d3 = tran.HashExistsAsync(hashkey, "key3");

            tran.Execute();

            (await s1).Should().BeTrue();
            (await s2).Should().BeTrue();
            (await s3).Should().BeTrue();

            (await removed).Should().Be(2);

            (await d1).Should().BeFalse();
            (await d2).Should().BeTrue();
            (await d3).Should().BeFalse();
        }
    }

    /// <summary>
    /// Tests for <see href="https://redis.io/commands/hexists"/>.
    /// </summary>
    [Fact]
    public async Task test_exists()
    {
        //Arrange
        await using var conn = Create();
        var db = conn.GetDatabase();
        var hashkey = Me();
        _ = db.KeyDeleteAsync(hashkey).ForAwait();
        var ex0 = db.HashExistsAsync(hashkey, "field").ForAwait();
        _ = db.HashSetAsync(hashkey, "field", "value").ForAwait();
        var ex1 = db.HashExistsAsync(hashkey, "field").ForAwait();
        _ = db.HashDeleteAsync(hashkey, "field").ForAwait();

        //Act
        _ = db.HashExistsAsync(hashkey, "field").ForAwait();

        //Assert
        (await ex0).Should().BeFalse();
        (await ex1).Should().BeTrue();
        (await ex0).Should().BeFalse();
    }

    /// <summary>
    /// Tests for <see href="https://redis.io/commands/hkeys"/>.
    /// </summary>
    [Fact]
    public async Task test_hash_keys()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var hashKey = Me();
        await db.KeyDeleteAsync(hashKey).ForAwait();

        var keys0 = await db.HashKeysAsync(hashKey).ForAwait();
        keys0.Should().BeEmpty();

        await db.HashSetAsync(hashKey, "foo", "abc").ForAwait();
        await db.HashSetAsync(hashKey, "bar", "def").ForAwait();

        var keys1 = db.HashKeysAsync(hashKey);

        var arr = await keys1;
        arr.Length.Should().Be(2);
        arr[0].Should().Be("foo");
        arr[1].Should().Be("bar");
    }

    /// <summary>
    /// Tests for <see href="https://redis.io/commands/hvals"/>.
    /// </summary>
    [Fact]
    public async Task test_hash_values()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var hashkey = Me();
        await db.KeyDeleteAsync(hashkey).ForAwait();

        var keys0 = await db.HashValuesAsync(hashkey).ForAwait();

        await db.HashSetAsync(hashkey, "foo", "abc").ForAwait();
        await db.HashSetAsync(hashkey, "bar", "def").ForAwait();

        var keys1 = db.HashValuesAsync(hashkey).ForAwait();

        keys0.Should().BeEmpty();

        var arr = await keys1;
        arr.Length.Should().Be(2);
        Encoding.UTF8.GetString(arr[0]!).Should().Be("abc");
        Encoding.UTF8.GetString(arr[1]!).Should().Be("def");
    }

    /// <summary>
    /// Tests for <see href="https://redis.io/commands/hlen"/>.
    /// </summary>
    [Fact]
    public async Task test_hash_length()
    {
        //Arrange
        await using var conn = Create();
        var db = conn.GetDatabase();
        var hashkey = Me();
        db.KeyDelete(hashkey, CommandFlags.FireAndForget);
        var len0 = db.HashLengthAsync(hashkey);
        db.HashSet(hashkey, "foo", "abc", flags: CommandFlags.FireAndForget);
        db.HashSet(hashkey, "bar", "def", flags: CommandFlags.FireAndForget);

        //Act
        var len1 = db.HashLengthAsync(hashkey);

        //Assert
        (await len0).Should().Be(0);
        (await len1).Should().Be(2);
    }

    /// <summary>
    /// Tests for <see href="https://redis.io/commands/hmget"/>.
    /// </summary>
    [Fact]
    public async Task test_get_multi()
    {
        //Arrange
        await using var conn = Create();
        var db = conn.GetDatabase();
        var hashkey = Me();
        db.KeyDelete(hashkey, CommandFlags.FireAndForget);
        RedisValue[] fields = ["foo", "bar", "blop"];
        var arr0 = await db.HashGetAsync(hashkey, fields).ForAwait();
        db.HashSet(hashkey, "foo", "abc", flags: CommandFlags.FireAndForget);
        db.HashSet(hashkey, "bar", "def", flags: CommandFlags.FireAndForget);
        var arr1 = await db.HashGetAsync(hashkey, fields).ForAwait();

        //Act
        var arr2 = await db.HashGetAsync(hashkey, fields).ForAwait();

        //Assert
        arr0.Length.Should().Be(3);
        ((string?)arr0[0]).Should().BeNull();
        ((string?)arr0[1]).Should().BeNull();
        ((string?)arr0[2]).Should().BeNull();
        arr1.Length.Should().Be(3);
        arr1[0].Should().Be("abc");
        arr1[1].Should().Be("def");
        ((string?)arr1[2]).Should().BeNull();
        arr2.Length.Should().Be(3);
        arr2[0].Should().Be("abc");
        arr2[1].Should().Be("def");
        ((string?)arr2[2]).Should().BeNull();
    }

    /// <summary>
    /// Tests for <see href="https://redis.io/commands/hgetall"/>.
    /// </summary>
    [Fact]
    public async Task test_get_pairs()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var hashkey = Me();
        _ = db.KeyDeleteAsync(hashkey);

        var result0 = db.HashGetAllAsync(hashkey);

        _ = db.HashSetAsync(hashkey, "foo", "abc");
        _ = db.HashSetAsync(hashkey, "bar", "def");

        var result1 = db.HashGetAllAsync(hashkey);

        conn.Wait(result0).Should().BeEmpty();
        var result = conn.Wait(result1).ToStringDictionary();
        result.Count.Should().Be(2);
        result["foo"].Should().Be("abc");
        result["bar"].Should().Be("def");
    }

    /// <summary>
    /// Tests for <see href="https://redis.io/commands/hmset"/>.
    /// </summary>
    [Fact]
    public async Task test_set_pairs()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var hashkey = Me();
        _ = db.KeyDeleteAsync(hashkey).ForAwait();

        var result0 = db.HashGetAllAsync(hashkey);

        var data = new[]
        {
            new HashEntry("foo", Encoding.UTF8.GetBytes("abc")),
            new HashEntry("bar", Encoding.UTF8.GetBytes("def")),
        };
        _ = db.HashSetAsync(hashkey, data).ForAwait();

        var result1 = db.Wait(db.HashGetAllAsync(hashkey));

        result0.Result.Should().BeEmpty();
        var result = result1.ToStringDictionary();
        result.Count.Should().Be(2);
        result["foo"].Should().Be("abc");
        result["bar"].Should().Be("def");
    }

    [Fact]
    public async Task test_when_always_async()
    {
        await using var conn = Create();

        var db = conn.GetDatabase();
        var hashkey = Me();
        db.KeyDelete(hashkey, CommandFlags.FireAndForget);

        var result1 = await db.HashSetAsync(hashkey, "foo", "bar", When.Always, CommandFlags.None);
        var result2 = await db.HashSetAsync(hashkey, "foo2", "bar", When.Always, CommandFlags.None);
        var result3 = await db.HashSetAsync(hashkey, "foo", "bar", When.Always, CommandFlags.None);
        var result4 = await db.HashSetAsync(hashkey, "foo", "bar2", When.Always, CommandFlags.None);

        result1.Should().BeTrue("Initial set key 1");
        result2.Should().BeTrue("Initial set key 2");
        // Fields modified *but not added* should be a zero/false. That's the behavior of HSET
        result3.Should().BeFalse("Duplicate set key 1");
        result4.Should().BeFalse("Duplicate se key 1 variant");
    }

    [Fact]
    public async Task hash_random_field_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var hashKey = Me();
        var items = new HashEntry[] { new("new york", "yankees"), new("baltimore", "orioles"), new("boston", "red sox"), new("Tampa Bay", "rays"), new("Toronto", "blue jays") };
        await db.HashSetAsync(hashKey, items);

        var singleField = await db.HashRandomFieldAsync(hashKey);
        var multiFields = await db.HashRandomFieldsAsync(hashKey, 3);
        var withValues = await db.HashRandomFieldsWithValuesAsync(hashKey, 3);
        multiFields.Length.Should().Be(3);
        withValues.Length.Should().Be(3);
        items.Should().Contain(x => x.Name == singleField);

        foreach (var field in multiFields)
        {
            items.Should().Contain(x => x.Name == field);
        }

        foreach (var field in withValues)
        {
            items.Should().Contain(x => x.Name == field.Name);
        }
    }

    [Fact]
    public async Task hash_random_field()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var db = conn.GetDatabase();
        var hashKey = Me();
        var items = new HashEntry[] { new("new york", "yankees"), new("baltimore", "orioles"), new("boston", "red sox"), new("Tampa Bay", "rays"), new("Toronto", "blue jays") };
        db.HashSet(hashKey, items);

        var singleField = db.HashRandomField(hashKey);
        var multiFields = db.HashRandomFields(hashKey, 3);
        var withValues = db.HashRandomFieldsWithValues(hashKey, 3);
        multiFields.Length.Should().Be(3);
        withValues.Length.Should().Be(3);
        items.Should().Contain(x => x.Name == singleField);

        foreach (var field in multiFields)
        {
            items.Should().Contain(x => x.Name == field);
        }

        foreach (var field in withValues)
        {
            items.Should().Contain(x => x.Name == field.Name);
        }
    }

    [Fact]
    public async Task hash_random_field_empty_hash()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var db = conn.GetDatabase();
        var hashKey = Me();
        var singleField = db.HashRandomField(hashKey);
        var multiFields = db.HashRandomFields(hashKey, 3);

        //Act
        var withValues = db.HashRandomFieldsWithValues(hashKey, 3);

        //Assert
        singleField.Should().Be(RedisValue.Null);
        multiFields.Should().BeEmpty();
        withValues.Should().BeEmpty();
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;
using Xunit.Sdk;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

// building on array.tcl from the redis tests
[RunPerProtocol]
public class ArrayTests(SharedConnectionFixture fixture, ITestOutputHelper log)
    : TestBase(log, fixture)
{
    [Fact]
    public async Task basic_set_get_tests()
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        RedisKey missing = WithSuffix(key, ":missing");
        await db.KeyDeleteAsync([key, missing]);

        (await db.ArraySetAsync(key, 0, "hello")).Should().BeTrue();
        (await db.ArrayGetAsync(key, 0)).Should().Be("hello");
        (await db.ArrayGetAsync(key, 1)).Should().Be(RedisValue.Null);

        (await db.ArraySetAsync(key, 0, "world")).Should().BeFalse();
        (await db.ArrayGetAsync(key, 0)).Should().Be("world");

        (await db.ArrayGetAsync(missing, 0)).Should().Be(RedisValue.Null);

        (await db.ArraySetAsync(key, 10, 12345)).Should().BeTrue();
        (await db.ArrayGetAsync(key, 10)).Should().Be("12345");

        (await db.ArraySetAsync(key, 11, 3.14159)).Should().BeTrue();
        var floatValue = await db.ArrayGetAsync(key, 11);
        ((double)floatValue).Should().BeApproximately(3.14159, 0.000005);

        (await db.ArraySetAsync(key, 12, "abc")).Should().BeTrue();
        (await db.ArrayGetAsync(key, 12)).Should().Be("abc");

        var longString = new string('x', 100);
        (await db.ArraySetAsync(key, 13, longString)).Should().BeTrue();
        (await db.ArrayGetAsync(key, 13)).Should().Be(longString);

        (await db.ArraySetAsync(key, 14, RedisValue.EmptyString)).Should().BeTrue();
        (await db.ArrayGetAsync(key, 14)).Should().Be(RedisValue.EmptyString);
    }

    [Fact]
    public async Task length_count_and_sparse_gaps()
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        await db.KeyDeleteAsync(key);

        AssertIndex(await db.ArrayLengthAsync(key), 0);
        AssertIndex(await db.ArrayCountAsync(key), 0);

        (await db.ArraySetAsync(key, 0, "a")).Should().BeTrue();
        AssertIndex(await db.ArrayLengthAsync(key), 1);
        AssertIndex(await db.ArrayCountAsync(key), 1);

        (await db.ArraySetAsync(key, 5, "b")).Should().BeTrue();
        AssertIndex(await db.ArrayLengthAsync(key), 6);
        AssertIndex(await db.ArrayCountAsync(key), 2);

        (await db.ArraySetAsync(key, 100, "c")).Should().BeTrue();
        AssertIndex(await db.ArrayLengthAsync(key), 101);
        AssertIndex(await db.ArrayCountAsync(key), 3);

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, 0, "a")).Should().BeTrue();
        (await db.ArraySetAsync(key, 10000, "b")).Should().BeTrue();
        (await db.ArraySetAsync(key, 1000000, "c")).Should().BeTrue();

        (await db.ArrayGetAsync(key, 0)).Should().Be("a");
        (await db.ArrayGetAsync(key, 10000)).Should().Be("b");
        (await db.ArrayGetAsync(key, 1000000)).Should().Be("c");
        AssertIndex(await db.ArrayCountAsync(key), 3);
        AssertIndex(await db.ArrayLengthAsync(key), 1000001);
    }

    [Fact]
    public async Task delete_and_delete_range()
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        await db.KeyDeleteAsync(key);

        (await db.ArraySetAsync(key, 0, ["a", "b", "c"])).Should().Be(3);
        (await db.ArrayDeleteAsync(key, 1)).Should().BeTrue();
        (await db.ArrayGetAsync(key, 1)).Should().Be(RedisValue.Null);
        AssertIndex(await db.ArrayCountAsync(key), 2);
        (await db.ArrayDeleteAsync(key, 1)).Should().BeFalse();

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, 0, ["a", "b", "c", "d"])).Should().Be(4);
        (await db.ArrayDeleteAsync(key, [0, 1, 2])).Should().Be(3);
        AssertIndex(await db.ArrayCountAsync(key), 1);

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, 0, "a")).Should().BeTrue();
        (await db.ArrayDeleteAsync(key, 0)).Should().BeTrue();
        (await db.KeyExistsAsync(key)).Should().BeFalse();

        await db.KeyDeleteAsync(key);
        await SetNumericValuesAsync(db, key, 10);
        AssertIndex(await db.ArrayCountAsync(key), 10);
        AssertIndex(await db.ArrayDeleteRangeAsync(key, 2, 6), 5);
        AssertIndex(await db.ArrayCountAsync(key), 5);

        await db.KeyDeleteAsync(key);
        await SetNumericValuesAsync(db, key, 10);
        AssertIndex(await db.ArrayDeleteRangeAsync(key, 6, 2), 5);
        AssertIndex(await db.ArrayCountAsync(key), 5);

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, 0, ["a", "b", "c", "d", "e", "f"])).Should().Be(6);
        AssertIndex(await db.ArrayDeleteRangeAsync(key, [new RedisArrayRange(0, 1), new RedisArrayRange(4, 5)]), 4);
        AssertValues(await db.ArrayGetRangeAsync(key, 0, 5), RedisValue.Null, RedisValue.Null, "c", "d", RedisValue.Null, RedisValue.Null);
    }

    [Fact(Timeout = 10000)]
    public async Task delete_last_element_publishes_array_delete_before_key_delete_notifications()
    {
        #if !DEBUG
        Assert.Skip("Debug only due to parallelism overhead");
        #endif
        await using var conn = Create(allowAdmin: true, require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        await AssertArrayKeyspaceNotificationsEnabledAsync(conn);

        RedisKey key = Me();
        await db.KeyDeleteAsync(key);

        var sub = conn.GetSubscriber();
        var channel = RedisChannel.Pattern($"__key*@{db.Database}__:*");
        var queue = await sub.SubscribeAsync(channel);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        await sub.PingAsync();
        try
        {
            (await db.ArraySetAsync(key, 0, "a")).Should().BeTrue();
            (await db.ArrayDeleteAsync(key, 0)).Should().BeTrue();

            AssertNotification(await ReadNotificationAsync(queue, key), KeyNotificationKind.KeySpace, KeyNotificationType.ArDel);
            AssertNotification(await ReadNotificationAsync(queue, key), KeyNotificationKind.KeyEvent, KeyNotificationType.ArDel);
            AssertNotification(await ReadNotificationAsync(queue, key), KeyNotificationKind.KeySpace, KeyNotificationType.Del);
            AssertNotification(await ReadNotificationAsync(queue, key), KeyNotificationKind.KeyEvent, KeyNotificationType.Del);
        }
        finally
        {
            await queue.UnsubscribeAsync();
        }
    }

    [Fact]
    public async Task multi_set_multi_get_and_ranges()
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        await db.KeyDeleteAsync(key);

        (await db.ArraySetAsync(key, [Entry(0, "a"), Entry(1, "b"), Entry(2, "c")])).Should().Be(3);
        (await db.ArrayGetAsync(key, 0)).Should().Be("a");
        (await db.ArrayGetAsync(key, 1)).Should().Be("b");
        (await db.ArrayGetAsync(key, 2)).Should().Be("c");

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, 0, "a")).Should().BeTrue();
        (await db.ArraySetAsync(key, [Entry(0, "aa"), Entry(1, "b")])).Should().Be(1);
        (await db.ArrayGetAsync(key, 0)).Should().Be("aa");
        (await db.ArrayGetAsync(key, 1)).Should().Be("b");

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, [Entry(0, "a"), Entry(1, "b"), Entry(5, "c")])).Should().Be(3);
        AssertValues(await db.ArrayGetAsync(key, [0, 1, 5, 3]), "a", "b", "c", RedisValue.Null);

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, [Entry(0, "a"), Entry(1, "b"), Entry(2, "c"), Entry(3, "d"), Entry(4, "e")])).Should().Be(5);
        AssertValues(await db.ArrayGetRangeAsync(key, 1, 3), "b", "c", "d");
        AssertValues(await db.ArrayGetRangeAsync(key, 3, 1), "d", "c", "b");

        await AssertServerErrorAsync("range exceeds maximum", async () => _ = await db.ArrayGetRangeAsync(key, 0, 1000000));
        await AssertServerErrorAsync("range exceeds maximum", async () => _ = await db.ArrayGetRangeAsync(key, 1000000, 0));

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, 0, ["a", "b", "c"])).Should().Be(3);
        (await db.ArrayGetAsync(key, 0)).Should().Be("a");
        (await db.ArrayGetAsync(key, 1)).Should().Be("b");
        (await db.ArrayGetAsync(key, 2)).Should().Be("c");
    }

    [Fact]
    public async Task scan()
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        RedisKey missing = WithSuffix(key, ":missing");
        await db.KeyDeleteAsync([key, missing]);

        (await db.ArraySetAsync(key, [Entry(0, "a"), Entry(5, "b"), Entry(9, "c")])).Should().Be(3);
        AssertEntries(await db.ArrayScanAsync(key, 0, 10), Entry(0, "a"), Entry(5, "b"), Entry(9, "c"));

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, 500, "x")).Should().BeTrue();
        (await db.ArrayScanAsync(key, 0, 100)).Should().BeEmpty();

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, [Entry(0, "a"), Entry(5, "b")])).Should().Be(2);
        AssertEntries(await db.ArrayScanAsync(key, 5, 0), Entry(5, "b"), Entry(0, "a"));

        (await db.ArrayScanAsync(missing, 0, 100)).Should().BeEmpty();

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, [Entry(0, "string"), Entry(1, 12345), Entry(2, 3.14)])).Should().Be(3);
        AssertEntries(await db.ArrayScanAsync(key, 0, 10), Entry(0, "string"), Entry(1, "12345"), Entry(2, "3.14"));
    }

    [Fact]
    public async Task grep_basics()
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        RedisKey missing = WithSuffix(key, ":missing");
        await db.KeyDeleteAsync([key, missing]);

        (await db.ArraySetAsync(key, [Entry(0, "alpha"), Entry(1, "beta"), Entry(2, "alphabet"), Entry(5, "gamma")])).Should().Be(4);
        AssertIndexEntries(await db.ArrayGrepAsync(key, CreateGrep(ArrayGrepRequest.Predicate.Match("alpha"))), 0, 2);

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, [Entry(0, "alpha"), Entry(1, "beta"), Entry(2, "alphabet"), Entry(3, "delta")])).Should().Be(4);
        var withValues = CreateGrep(ArrayGrepRequest.Predicate.Match("alpha"));
        withValues.Start = 3;
        withValues.End = 0;
        withValues.IncludeValues = true;
        AssertEntries(await db.ArrayGrepAsync(key, withValues), Entry(2, "alphabet"), Entry(0, "alpha"));

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, [Entry(0, "RedisArray"), Entry(1, "redis-match"), Entry(2, "array-only"), Entry(3, "plain")])).Should().Be(4);
        var andNoCase = CreateGrep(ArrayGrepRequest.Predicate.Match("redis"), ArrayGrepRequest.Predicate.Glob("*array*"));
        andNoCase.IsIntersection = true;
        andNoCase.IsCaseInsensitive = true;
        AssertIndexEntries(await db.ArrayGrepAsync(key, andNoCase), 0);

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, [Entry(0, "hit-1"), Entry(1, "hit-2"), Entry(2, "miss"), Entry(3, "hit-3")])).Should().Be(4);
        var limited = CreateGrep(ArrayGrepRequest.Predicate.Match("hit"));
        limited.Limit = 2;
        AssertIndexEntries(await db.ArrayGrepAsync(key, limited), 0, 1);

        (await db.ArrayGrepAsync(missing, CreateGrep(ArrayGrepRequest.Predicate.Match("foo")))).Should().BeEmpty();
    }

    [Fact]
    public async Task grep_regex_and_errors()
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        await db.KeyDeleteAsync(key);

        (await db.ArraySetAsync(key, [Entry(0, "foo123"), Entry(1, "bar"), Entry(2, "zoo999"), Entry(3, "Foo777")])).Should().Be(4);
        AssertIndexEntries(await db.ArrayGrepAsync(key, CreateGrep(ArrayGrepRequest.Predicate.Regex("^.*[0-9]{3}$"))), 0, 2, 3);

        var noCase = CreateGrep(ArrayGrepRequest.Predicate.Regex("^foo[0-9]+$"));
        noCase.IsCaseInsensitive = true;
        AssertIndexEntries(await db.ArrayGrepAsync(key, noCase), 0, 3);

        await db.KeyDeleteAsync(key);
        var values = new RedisArrayEntry[]
        {
            Entry(0, "foo"), Entry(1, "bar"), Entry(2, "baz"), Entry(3, "foobar"), Entry(4, "BAR"),
            Entry(5, "quxfoo"), Entry(6, "zedbar"), Entry(7, "plain"), Entry(8, "ALPS"), Entry(9, "alphabet"),
        };
        (await db.ArraySetAsync(key, values)).Should().Be(10);

        AssertIndexEntries(await db.ArrayGrepAsync(key, CreateGrep(ArrayGrepRequest.Predicate.Regex("foo|bar"))), 0, 1, 3, 5, 6);
        noCase = CreateGrep(ArrayGrepRequest.Predicate.Regex("foo|bar"));
        noCase.IsCaseInsensitive = true;
        AssertIndexEntries(await db.ArrayGrepAsync(key, noCase), 0, 1, 3, 4, 5, 6);

        // and same again, with reversed start/end
        noCase = CreateGrep(ArrayGrepRequest.Predicate.Regex("foo|bar"));
        noCase.IsCaseInsensitive = true;
        noCase.IsReversed = true;
        AssertIndexEntries(await db.ArrayGrepAsync(key, noCase), 6, 5, 4, 3, 1, 0);

        noCase = CreateGrep(ArrayGrepRequest.Predicate.Regex("^(foo|bar)$"));
        noCase.IsCaseInsensitive = true;
        AssertIndexEntries(await db.ArrayGrepAsync(key, noCase), 0, 1, 4);

        noCase = CreateGrep(ArrayGrepRequest.Predicate.Regex("^(foo|bar)"));
        noCase.IsCaseInsensitive = true;
        AssertIndexEntries(await db.ArrayGrepAsync(key, noCase), 0, 1, 3, 4);

        noCase = CreateGrep(ArrayGrepRequest.Predicate.Regex("(foo|bar)$"));
        noCase.IsCaseInsensitive = true;
        AssertIndexEntries(await db.ArrayGrepAsync(key, noCase), 0, 1, 3, 4, 5, 6);

        noCase = CreateGrep(ArrayGrepRequest.Predicate.Regex("alpha|alps"));
        noCase.IsCaseInsensitive = true;
        AssertIndexEntries(await db.ArrayGrepAsync(key, noCase), 8, 9);

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, [Entry(0, "item-foo-123"), Entry(1, "ITEM-BAR-456"), Entry(2, "item-baz"), Entry(3, "plain")])).Should().Be(4);
        noCase = CreateGrep(ArrayGrepRequest.Predicate.Regex("^item-(foo|bar)-[0-9]{3}$"));
        noCase.IsCaseInsensitive = true;
        AssertIndexEntries(await db.ArrayGrepAsync(key, noCase), 0, 1);

        await db.KeyDeleteAsync(key);
        var re2048 = new string('a', 2048);
        var re2049 = new string('a', 2049);
        (await db.ArraySetAsync(key, 0, re2048)).Should().BeTrue();
        AssertIndexEntries(await db.ArrayGrepAsync(key, CreateGrep(ArrayGrepRequest.Predicate.Regex(re2048))), 0);
        await AssertServerErrorAsync("maximum is 2048 bytes", async () => _ = await db.ArrayGrepAsync(key, CreateGrep(ArrayGrepRequest.Predicate.Regex(re2049))));
        await AssertServerErrorAsync("backreferences are not supported", async () => _ = await db.ArrayGrepAsync(key, CreateGrep(ArrayGrepRequest.Predicate.Regex("(a)\\1"))));
        await AssertServerErrorAsync("regular expression is empty", async () => _ = await db.ArrayGrepAsync(key, CreateGrep(ArrayGrepRequest.Predicate.Regex(""))));

        await AssertServerErrorAsync("invalid regular expression", async () => _ = await db.ArrayGrepAsync(key, CreateGrep(ArrayGrepRequest.Predicate.Regex("\\x{1"))));

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, 0, "foo")).Should().BeTrue();
        var request = new ArrayGrepRequest();
        for (int i = 0; i < 250; i++)
        {
            request.AddPredicate(ArrayGrepRequest.Predicate.Match("foo"));
        }
        AssertIndexEntries(await db.ArrayGrepAsync(key, request), 0);

        request = new ArrayGrepRequest();
        for (int i = 0; i < 251; i++)
        {
            request.AddPredicate(ArrayGrepRequest.Predicate.Match("foo"));
        }
        await AssertServerErrorAsync("maximum is 250", async () => _ = await db.ArrayGrepAsync(key, request));
    }

    [Fact]
    public async Task insert_ring_next_seek_and_last_items()
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        RedisKey missing = WithSuffix(key, ":missing");
        await db.KeyDeleteAsync([key, missing]);

        AssertIndex(await db.ArrayInsertAsync(key, "a"), 0);
        AssertIndex(await db.ArrayInsertAsync(key, "b"), 1);
        AssertIndex(await db.ArrayInsertAsync(key, "c"), 2);
        (await db.ArrayGetAsync(key, 0)).Should().Be("a");
        (await db.ArrayGetAsync(key, 1)).Should().Be("b");
        (await db.ArrayGetAsync(key, 2)).Should().Be("c");

        await db.KeyDeleteAsync(key);
        for (int i = 0; i < 10; i++)
        {
            _ = await db.ArrayRingAsync(key, 5, i);
        }
        (await db.ArrayGetAsync(key, 0)).Should().Be("5");
        (await db.ArrayGetAsync(key, 1)).Should().Be("6");
        (await db.ArrayGetAsync(key, 2)).Should().Be("7");
        (await db.ArrayGetAsync(key, 3)).Should().Be("8");
        (await db.ArrayGetAsync(key, 4)).Should().Be("9");
        AssertIndex(await db.ArrayCountAsync(key), 5);

        await db.KeyDeleteAsync(key);
        AssertIndex(await db.ArrayNextAsync(key), 0);
        AssertIndex(await db.ArrayInsertAsync(key, "a"), 0);
        AssertIndex(await db.ArrayNextAsync(key), 1);
        AssertIndex(await db.ArrayInsertAsync(key, "b"), 1);
        AssertIndex(await db.ArrayNextAsync(key), 2);

        (await db.ArraySeekAsync(missing, 10)).Should().BeFalse();
        (await db.ArraySeekAsync(key, 10)).Should().BeTrue();
        AssertIndex(await db.ArrayInsertAsync(key, "c"), 10);
        AssertIndex(await db.ArrayNextAsync(key), 11);
        (await db.ArrayGetAsync(key, 10)).Should().Be("c");

        await db.KeyDeleteAsync(key);
        AssertIndex(await db.ArrayInsertAsync(key, "a"), 0);
        (await db.ArraySeekAsync(key, RedisArrayIndex.MaxValue)).Should().BeTrue();
        (await db.ArrayNextAsync(key)).Should().BeNull();
        await AssertServerErrorAsync("insert index overflow", async () => _ = await db.ArrayInsertAsync(key, "b"));

        await db.KeyDeleteAsync(key);
        for (int i = 0; i < 5; i++)
        {
            _ = await db.ArrayInsertAsync(key, i * 10);
        }
        AssertValues(await db.ArrayLastItemsAsync(key, 3), "20", "30", "40");
        AssertValues(await db.ArrayLastItemsAsync(key, 3, reverse: true), "40", "30", "20");

        (await db.ArraySeekAsync(key, 0)).Should().BeTrue();
        AssertValues(await db.ArrayLastItemsAsync(key, 3), "20", "30", "40");
        AssertValues(await db.ArrayLastItemsAsync(key, 3, reverse: true), "40", "30", "20");
    }

    [Fact]
    public async Task array_operations()
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        await db.KeyDeleteAsync(key);

        (await db.ArraySetAsync(key, [Entry(0, 10), Entry(1, 20), Entry(2, 30)])).Should().Be(3);
        (await ArrayOperationInt64Async(db, key, 0, 2, ArrayOperation.Sum)).Should().Be(60);
        await Assert.ThrowsAsync<ArgumentException>(async () => _ = await db.ArrayOperationAsync(key, 0, 2, ArrayOperation.Match));
        await Assert.ThrowsAsync<ArgumentException>(async () => _ = await db.ArrayOperationAsync(key, 0, 2, ArrayOperation.Sum, "value"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => _ = await db.ArrayOperationAsync(key, 0, 2, ArrayOperation.Unknown));

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, [Entry(0, 30), Entry(1, 10), Entry(2, 20)])).Should().Be(3);
        (await ArrayOperationInt64Async(db, key, 0, 2, ArrayOperation.Min)).Should().Be(10);
        (await ArrayOperationInt64Async(db, key, 0, 2, ArrayOperation.Max)).Should().Be(30);

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, [Entry(0, "hello"), Entry(1, "world"), Entry(2, "hello"), Entry(3, "foo")])).Should().Be(4);
        (await ArrayOperationInt64Async(db, key, 0, 3, ArrayOperation.Match, "hello")).Should().Be(2);
        (await ArrayOperationInt64Async(db, key, 0, 3, ArrayOperation.Match, "world")).Should().Be(1);
        (await ArrayOperationInt64Async(db, key, 0, 3, ArrayOperation.Match, "bar")).Should().Be(0);

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, [Entry(0, "a"), Entry(2, "b"), Entry(5, "c")])).Should().Be(3);
        (await ArrayOperationInt64Async(db, key, 0, 10, ArrayOperation.Used)).Should().Be(3);

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, [Entry(0, 255), Entry(1, 15), Entry(2, 240)])).Should().Be(3);
        (await ArrayOperationInt64Async(db, key, 0, 2, ArrayOperation.And)).Should().Be(0);
        (await ArrayOperationInt64Async(db, key, 0, 2, ArrayOperation.Or)).Should().Be(255);
        (await ArrayOperationInt64Async(db, key, 0, 2, ArrayOperation.Xor)).Should().Be(0);

        await db.KeyDeleteAsync(key);
        (await db.ArraySetAsync(key, [Entry(0, 7.9), Entry(1, 3.2), Entry(2, 1.8)])).Should().Be(3);
        (await ArrayOperationInt64Async(db, key, 0, 2, ArrayOperation.And)).Should().Be(1);
        (await ArrayOperationInt64Async(db, key, 0, 2, ArrayOperation.Or)).Should().Be(7);
        (await ArrayOperationInt64Async(db, key, 0, 2, ArrayOperation.Xor)).Should().Be(5);
    }

    [Fact]
    public async Task info_type_encoding_and_wrong_type()
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        RedisKey wrongType = WithSuffix(key, ":wrong");
        await db.KeyDeleteAsync([key, wrongType]);

        (await db.ArraySetAsync(key, [Entry(0, "a"), Entry(1, "b"), Entry(100, "c")])).Should().Be(3);
        var info = await db.ArrayInfoAsync(key);
        AssertIndex(info.Count, 3);
        AssertIndex(info.Length, 101);
        AssertIndex(info.NextInsertIndex, 0);
        AssertIndex(info.Slices, 1);
        AssertIndex(info.DirectorySize, 1);
        AssertIndex(info.SuperDirEntries, 0);
        AssertIndex(info.SliceSize, 4096);

        (await db.KeyTypeAsync(key)).Should().Be(RedisType.Array);
        (await db.KeyEncodingAsync(key)).Should().Be("sliced-array");

        (await db.StringSetAsync(wrongType, "value")).Should().BeTrue();
        await AssertServerErrorAsync("WRONGTYPE", async () => _ = await db.ArrayGetAsync(wrongType, 0));
        await AssertServerErrorAsync("WRONGTYPE", async () => _ = await db.ArraySetAsync(wrongType, 0, "foo"));
        await AssertServerErrorAsync("WRONGTYPE", async () => _ = await db.ArrayLengthAsync(wrongType));
        await AssertServerErrorAsync("WRONGTYPE", async () => _ = await db.ArrayCountAsync(wrongType));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task info_to_dictionary(bool full)
    {
        await using var conn = Create(require: RedisFeatures.v8_8_0);
        var db = conn.GetDatabase();
        RedisKey key = Me();
        await db.KeyDeleteAsync(key);

        (await db.ArraySetAsync(key, [Entry(0, "a"), Entry(1, "b"), Entry(100, "c")])).Should().Be(3);

        var info = await db.ArrayInfoAsync(key, full);
        var dictionary = info.ToDictionary();
        LogDictionary(dictionary, $"ArrayInfo full={full}");

        AssertArrayInfoDictionaryKnownFields(dictionary);
        AssertIndex(info.Count, 3);
        AssertIndex(info.Length, 101);
        ((long)dictionary["count"]).Should().Be(3);
        ((long)dictionary["len"]).Should().Be(101);

        if (full)
        {
            dictionary.Keys.Should().Contain("sparse-slices");

            var basicDictionary = (await db.ArrayInfoAsync(key)).ToDictionary();
            basicDictionary.Keys.Should().NotContain("sparse-slices");
            LogFullOnlyFields(basicDictionary, dictionary);
        }
        else
        {
            dictionary.Keys.Should().NotContain("sparse-slices");
        }
    }

    private static RedisArrayEntry Entry(long index, RedisValue value) => new RedisArrayEntry(index, value);

    private static RedisKey WithSuffix(RedisKey key, string suffix) => (RedisKey)(key.ToString() + suffix);

    private static ArrayGrepRequest CreateGrep(params ArrayGrepRequest.Predicate[] predicates)
    {
        var request = new ArrayGrepRequest();
        foreach (var predicate in predicates)
        {
            request.AddPredicate(predicate);
        }

        return request;
    }

    private static async Task SetNumericValuesAsync(IDatabaseAsync db, RedisKey key, int count)
    {
        for (int i = 0; i < count; i++)
        {
            (await db.ArraySetAsync(key, i, i * 10)).Should().BeTrue();
        }
    }

    private static async Task<long> ArrayOperationInt64Async(
        IDatabaseAsync db,
        RedisKey key,
        RedisArrayIndex start,
        RedisArrayIndex end,
        ArrayOperation operation,
        RedisValue operand = default)
    {
        var result = await db.ArrayOperationAsync(key, start, end, operation, operand);
        return (long)result;
    }

    private static void AssertIndex(RedisArrayIndex actual, ulong expected)
    {
        actual.Value.Should().Be(expected);
    }

    private static void AssertIndex(RedisArrayIndex? actual, ulong expected)
    {
        actual.HasValue.Should().BeTrue();
        actual.GetValueOrDefault().Value.Should().Be(expected);
    }

    private static void AssertArrayInfoDictionaryKnownFields(Dictionary<string, RedisValue> dictionary)
    {
        dictionary.Keys.Should().Contain("count");
        dictionary.Keys.Should().Contain("len");
        dictionary.Keys.Should().Contain("next-insert-index");
        dictionary.Keys.Should().Contain("slices");
        dictionary.Keys.Should().Contain("directory-size");
        dictionary.Keys.Should().Contain("super-dir-entries");
        dictionary.Keys.Should().Contain("slice-size");
    }

    private void LogDictionary(Dictionary<string, RedisValue> dictionary, string caption)
    {
        Log($"{caption}: {dictionary.Count} field(s)");
        var keys = new List<string>(dictionary.Keys);
        keys.Sort(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            Log($"  {key}: {dictionary[key]}");
        }
    }

    private void LogFullOnlyFields(Dictionary<string, RedisValue> basicDictionary, Dictionary<string, RedisValue> fullDictionary)
    {
        var keys = new List<string>();
        foreach (var key in fullDictionary.Keys)
        {
            if (!basicDictionary.ContainsKey(key))
            {
                keys.Add(key);
            }
        }

        keys.Sort(StringComparer.Ordinal);
        if (keys.Count == 0)
        {
            Log("ArrayInfo full-only fields: (none)");
        }
        else
        {
            Log($"ArrayInfo full-only fields: {keys.Count}");
            foreach (var key in keys)
            {
                Log($"  {key}: {fullDictionary[key]}");
            }
        }
    }

    private static void AssertIndexEntries(RedisArrayEntry[] actual, params ulong[] expected)
    {
        actual.Length.Should().Be(expected.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            actual[i].Index.Value.Should().Be(expected[i]);
            actual[i].Value.Should().Be(RedisValue.Null);
        }
    }

    private static void AssertEntries(RedisArrayEntry[] actual, params RedisArrayEntry[] expected)
    {
        actual.Length.Should().Be(expected.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            actual[i].Index.Value.Should().Be(expected[i].Index.Value);
            actual[i].Value.Should().Be(expected[i].Value);
        }
    }

    private static void AssertValues(RedisValue[] actual, params RedisValue[] expected)
    {
        actual.Length.Should().Be(expected.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            actual[i].Should().Be(expected[i]);
        }
    }

    private async Task<(KeyNotificationKind Kind, KeyNotificationType Type)> ReadNotificationAsync(ChannelMessageQueue queue, RedisKey key)
    {
        // there might be a lot of parallel notifications happening from parallel tests; as such, we might need to skip a lot of unrelated
        // stuff - allow for the timeout
        var ct = TestContext.Current.CancellationToken;
        while (!ct.IsCancellationRequested)
        {
            var message = await queue.ReadAsync(ct);
            if (message.TryParseKeyNotification(out var notification))
            {
                Log($"{notification.Kind}, {notification.Type} {message}");
                if (notification.GetKey() == key
                    && notification.Type is KeyNotificationType.ArDel or KeyNotificationType.Del)
                {
                    return (notification.Kind, notification.Type);
                }
            }
            else
            {
                Log($"Unable to parse: {message}");
            }
        }

        Assert.Fail($"Timed out waiting for array keyspace notifications for '{key}'.");
        return default;
    }

    private static void AssertNotification(
        (KeyNotificationKind Kind, KeyNotificationType Type) actual,
        KeyNotificationKind expectedKind,
        KeyNotificationType expectedType)
    {
        actual.Kind.Should().Be(expectedKind);
        actual.Type.Should().Be(expectedType);
    }

    private static async Task AssertArrayKeyspaceNotificationsEnabledAsync(IConnectionMultiplexer muxer)
    {
        foreach (var ep in muxer.GetEndPoints())
        {
            var server = muxer.GetServer(ep);
            var config = await server.ConfigGetAsync("notify-keyspace-events");
            var value = config.Length == 0 ? "" : config[0].Value.ToString() ?? "";

            foreach (var token in "AKE")
            {
                Assert.SkipUnless(
                    value.IndexOf(token) >= 0,
                    $"Server {ep} notify-keyspace-events config '{value}' missing required token '{token}' for array keyspace notifications.");
            }
        }
    }

    private static async Task AssertServerErrorAsync(string expectedMessage, Func<Task> action)
    {
        var ex = await Assert.ThrowsAsync<RedisServerException>(action);
        Assert.Contains(expectedMessage, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

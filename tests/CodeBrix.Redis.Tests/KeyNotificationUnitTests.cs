using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using SilverAssertions;
using Xunit;
using Xunit.Sdk;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class KeyNotificationUnitTests(ITestOutputHelper log)
{
    [Theory]
    [InlineData("foo", "foo")]
    [InlineData("__foo__", "__foo__")]
    [InlineData("__keyspace@4__:", "__keyspace@4__:")] // not long enough
    [InlineData("__keyspace@4__:f", "f")]
    [InlineData("__keyspace@4__:fo", "fo")]
    [InlineData("__keyspace@4__:foo", "foo")]
    [InlineData("__keyspace@42__:foo", "foo")] // check multi-char db
    [InlineData("__keyevent@4__:foo", "__keyevent@4__:foo")] // key-event
    [InlineData("__keyevent@42__:foo", "__keyevent@42__:foo")] // key-event
    public void routing_span_strip_key_space_prefix(string raw, string routed)
    {
        //Arrange
        ReadOnlySpan<byte> srcBytes = Encoding.UTF8.GetBytes(raw);
        var strippedBytes = RedisChannel.StripKeySpacePrefix(srcBytes);

        //Act
        var result = Encoding.UTF8.GetString(strippedBytes);

        //Assert
        result.Should().Be(routed);
    }

    [Fact]
    public void keyspace_del_parses_correctly()
    {
        //Arrange
        // __keyspace@1__:mykey with payload "del"
        var channel = RedisChannel.Literal("__keyspace@1__:mykey");
        channel.IgnoreChannelPrefix.Should().BeFalse();
        // because constructed manually
        RedisValue value = "del";
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeySpace);
        notification.Database.Should().Be(1);
        notification.Type.Should().Be(KeyNotificationType.Del);
        notification.IsType("del"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("mykey");
        notification.GetKeyByteCount().Should().Be(5);
        notification.GetKeyMaxByteCount().Should().Be(5);
        notification.GetKeyCharCount().Should().Be(5);
        notification.GetKeyMaxCharCount().Should().Be(6);
        // Test TryCopyKey (bytes)
        Span<byte> keyBuffer = stackalloc byte[10];
        notification.TryCopyKey(keyBuffer, out var bytesWritten).Should().BeTrue();
        bytesWritten.Should().Be(5);
        Encoding.UTF8.GetString(keyBuffer.Slice(0, bytesWritten)).Should().Be("mykey");

        //Act
        // Test TryCopyKey (chars)
        Span<char> charBuffer = stackalloc char[10];

        //Assert
        notification.TryCopyKey(charBuffer, out var charsWritten).Should().BeTrue();
        charsWritten.Should().Be(5);
        (new string(charBuffer.Slice(0, charsWritten).ToArray())).Should().Be("mykey");
    }

    [Fact]
    public void keyevent_del_parses_correctly()
    {
        //Arrange
        // __keyevent@42__:del with value "mykey"
        var channel = RedisChannel.Literal("__keyevent@42__:del");

        //Act
        RedisValue value = "mykey";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeyEvent);
        notification.Database.Should().Be(42);
        notification.Type.Should().Be(KeyNotificationType.Del);
        notification.IsType("del"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("mykey");
        notification.GetKeyByteCount().Should().Be(5);
        notification.GetKeyMaxByteCount().Should().Be(18);
        notification.GetKeyCharCount().Should().Be(5);
        notification.GetKeyMaxCharCount().Should().Be(5);
    }

    [Fact]
    public void keyspace_set_parses_correctly()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyspace@0__:testkey");

        //Act
        RedisValue value = "set";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeySpace);
        notification.Database.Should().Be(0);
        notification.Type.Should().Be(KeyNotificationType.Set);
        notification.IsType("set"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("testkey");
        notification.GetKeyByteCount().Should().Be(7);
        notification.GetKeyMaxByteCount().Should().Be(7);
        notification.GetKeyCharCount().Should().Be(7);
        notification.GetKeyMaxCharCount().Should().Be(8);
    }

    [Fact]
    public void keyevent_expire_parses_correctly()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyevent@5__:expire");

        //Act
        RedisValue value = "session:12345";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeyEvent);
        notification.Database.Should().Be(5);
        notification.Type.Should().Be(KeyNotificationType.Expire);
        notification.IsType("expire"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("session:12345");
        notification.GetKeyByteCount().Should().Be(13);
        notification.GetKeyMaxByteCount().Should().Be(42);
        notification.GetKeyCharCount().Should().Be(13);
        notification.GetKeyMaxCharCount().Should().Be(13);
    }

    [Fact]
    public void keyspace_expired_parses_correctly()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyspace@3__:cache:item");

        //Act
        RedisValue value = "expired";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeySpace);
        notification.Database.Should().Be(3);
        notification.Type.Should().Be(KeyNotificationType.Expired);
        notification.IsType("expired"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("cache:item");
        notification.GetKeyByteCount().Should().Be(10);
        notification.GetKeyMaxByteCount().Should().Be(10);
        notification.GetKeyCharCount().Should().Be(10);
        notification.GetKeyMaxCharCount().Should().Be(11);
    }

    [Fact]
    public void keyevent_l_push_parses_correctly()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyevent@0__:lpush");

        //Act
        RedisValue value = "queue:tasks";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeyEvent);
        notification.Database.Should().Be(0);
        notification.Type.Should().Be(KeyNotificationType.LPush);
        notification.IsType("lpush"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("queue:tasks");
        notification.GetKeyByteCount().Should().Be(11);
        notification.GetKeyMaxByteCount().Should().Be(36);
        notification.GetKeyCharCount().Should().Be(11);
        notification.GetKeyMaxCharCount().Should().Be(11);
    }

    [Fact]
    public void keyspace_h_set_parses_correctly()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyspace@2__:user:1000");

        //Act
        RedisValue value = "hset";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeySpace);
        notification.Database.Should().Be(2);
        notification.Type.Should().Be(KeyNotificationType.HSet);
        notification.IsType("hset"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("user:1000");
        notification.GetKeyByteCount().Should().Be(9);
        notification.GetKeyMaxByteCount().Should().Be(9);
        notification.GetKeyCharCount().Should().Be(9);
        notification.GetKeyMaxCharCount().Should().Be(10);
    }

    [Fact]
    public void keyevent_z_add_parses_correctly()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyevent@7__:zadd");

        //Act
        RedisValue value = "leaderboard";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeyEvent);
        notification.Database.Should().Be(7);
        notification.Type.Should().Be(KeyNotificationType.ZAdd);
        notification.IsType("zadd"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("leaderboard");
        notification.GetKeyByteCount().Should().Be(11);
        notification.GetKeyMaxByteCount().Should().Be(36);
        notification.GetKeyCharCount().Should().Be(11);
        notification.GetKeyMaxCharCount().Should().Be(11);
    }

    [Fact]
    public void custom_event_with_unusual_value_works()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyevent@7__:flooble");

        //Act
        RedisValue value = 17.5;

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeyEvent);
        notification.Database.Should().Be(7);
        notification.Type.Should().Be(KeyNotificationType.Unknown);
        notification.IsType("zadd"u8).Should().BeFalse();
        notification.IsType("flooble"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("17.5");
        notification.GetKeyByteCount().Should().Be(4);
        notification.GetKeyMaxByteCount().Should().Be(40);
        notification.GetKeyCharCount().Should().Be(4);
        notification.GetKeyMaxCharCount().Should().Be(40);
    }

    [Fact]
    public void try_copy_key_works_correctly()
    {
        var channel = RedisChannel.Literal("__keyspace@0__:testkey");
        RedisValue value = "set";

        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();

        var lease = ArrayPool<byte>.Shared.Rent(20);
        Span<byte> buffer = lease.AsSpan(0, 20);
        notification.TryCopyKey(buffer, out var bytesWritten).Should().BeTrue();
        bytesWritten.Should().Be(7);
        Encoding.UTF8.GetString(lease, 0, bytesWritten).Should().Be("testkey");
        ArrayPool<byte>.Shared.Return(lease);
    }

    [Fact]
    public void try_copy_key_fails_with_small_buffer()
    {
        var channel = RedisChannel.Literal("__keyspace@0__:testkey");
        RedisValue value = "set";

        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();

        Span<byte> buffer = stackalloc byte[3]; // too small
        notification.TryCopyKey(buffer, out var bytesWritten).Should().BeFalse();
        bytesWritten.Should().Be(7); // Should report the actual size needed (length of "testkey")
    }

    [Fact]
    public void invalid_channel_returns_false()
    {
        //Arrange
        var channel = RedisChannel.Literal("regular:channel");

        //Act
        RedisValue value = "data";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeFalse();
    }

    [Fact]
    public void invalid_keyspace_channel_missing_delimiter_returns_false()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyspace@0__");

        //Act
        // missing the key part
        RedisValue value = "set";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeFalse();
    }

    [Fact]
    public void keyspace_unknown_event_type_returns_unknown()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyspace@0__:mykey");

        //Act
        RedisValue value = "unknownevent";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeySpace);
        notification.Database.Should().Be(0);
        notification.Type.Should().Be(KeyNotificationType.Unknown);
        notification.IsType("del"u8).Should().BeFalse();
        ((string?)notification.GetKey()).Should().Be("mykey");
    }

    [Fact]
    public void keyevent_unknown_event_type_returns_unknown()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyevent@0__:unknownevent");

        //Act
        RedisValue value = "mykey";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeyEvent);
        notification.Database.Should().Be(0);
        notification.Type.Should().Be(KeyNotificationType.Unknown);
        notification.IsType("del"u8).Should().BeFalse();
        ((string?)notification.GetKey()).Should().Be("mykey");
    }

    [Fact]
    public void keyspace_with_colon_in_key_parses_correctly()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyspace@0__:user:session:12345");

        //Act
        RedisValue value = "del";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeySpace);
        notification.Database.Should().Be(0);
        notification.Type.Should().Be(KeyNotificationType.Del);
        notification.IsType("del"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("user:session:12345");
    }

    [Fact]
    public void keyevent_evicted_parses_correctly()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyevent@1__:evicted");

        //Act
        RedisValue value = "cache:old";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeyEvent);
        notification.Database.Should().Be(1);
        notification.Type.Should().Be(KeyNotificationType.Evicted);
        notification.IsType("evicted"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("cache:old");
    }

    [Fact]
    public void keyspace_new_parses_correctly()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyspace@0__:newkey");

        //Act
        RedisValue value = "new";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeySpace);
        notification.Database.Should().Be(0);
        notification.Type.Should().Be(KeyNotificationType.New);
        notification.IsType("new"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("newkey");
    }

    [Fact]
    public void keyevent_x_group_create_parses_correctly()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyevent@0__:xgroup-create");

        //Act
        RedisValue value = "mystream";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeyEvent);
        notification.Database.Should().Be(0);
        notification.Type.Should().Be(KeyNotificationType.XGroupCreate);
        notification.IsType("xgroup-create"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("mystream");
    }

    [Fact]
    public void keyspace_type_changed_parses_correctly()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyspace@0__:mykey");

        //Act
        RedisValue value = "type_changed";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeySpace);
        notification.Database.Should().Be(0);
        notification.Type.Should().Be(KeyNotificationType.TypeChanged);
        notification.IsType("type_changed"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("mykey");
    }

    [Fact]
    public void keyevent_high_database_number_parses_correctly()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyevent@999__:set");

        //Act
        RedisValue value = "testkey";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeyEvent);
        notification.Database.Should().Be(999);
        notification.Type.Should().Be(KeyNotificationType.Set);
        notification.IsType("set"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("testkey");
    }

    [Fact]
    public void keyevent_non_integer_database_parses_well_enough()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyevent@abc__:set");

        //Act
        RedisValue value = "testkey";

        //Assert
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeyEvent);
        notification.Database.Should().Be(-1);
        notification.Type.Should().Be(KeyNotificationType.Set);
        notification.IsType("set"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("testkey");
    }

    [Fact]
    public void default_key_notification_has_expected_properties()
    {
        //Arrange
        var notification = default(KeyNotification);
        notification.Kind.Should().Be(KeyNotificationKind.Unknown);
        notification.Database.Should().Be(-1);
        notification.Type.Should().Be(KeyNotificationType.Unknown);
        notification.IsType("del"u8).Should().BeFalse();
        notification.GetKey().IsNull.Should().BeTrue();
        notification.GetSubKeys().FirstOrDefault().IsNull.Should().BeTrue();
        notification.GetKeyByteCount().Should().Be(0);
        notification.GetKeyMaxByteCount().Should().Be(0);
        notification.GetKeyCharCount().Should().Be(0);
        notification.GetKeyMaxCharCount().Should().Be(0);
        notification.GetChannel().IsNull.Should().BeTrue();
        notification.GetValue().IsNull.Should().BeTrue();

        //Act
        // TryCopyKey should return false and write 0 bytes
        Span<byte> buffer = stackalloc byte[10];

        //Assert
        notification.TryCopyKey(buffer, out var bytesWritten).Should().BeFalse();
        bytesWritten.Should().Be(0);
    }

    [Theory]
    [InlineData("append", KeyNotificationType.Append)]
    [InlineData("copy", KeyNotificationType.Copy)]
    [InlineData("del", KeyNotificationType.Del)]
    [InlineData("expire", KeyNotificationType.Expire)]
    [InlineData("hdel", KeyNotificationType.HDel)]
    [InlineData("hexpired", KeyNotificationType.HExpired)]
    [InlineData("hincrbyfloat", KeyNotificationType.HIncrByFloat)]
    [InlineData("hincrby", KeyNotificationType.HIncrBy)]
    [InlineData("hpersist", KeyNotificationType.HPersist)]
    [InlineData("hset", KeyNotificationType.HSet)]
    [InlineData("incrbyfloat", KeyNotificationType.IncrByFloat)]
    [InlineData("incrby", KeyNotificationType.IncrBy)]
    [InlineData("linsert", KeyNotificationType.LInsert)]
    [InlineData("lpop", KeyNotificationType.LPop)]
    [InlineData("lpush", KeyNotificationType.LPush)]
    [InlineData("lrem", KeyNotificationType.LRem)]
    [InlineData("lset", KeyNotificationType.LSet)]
    [InlineData("ltrim", KeyNotificationType.LTrim)]
    [InlineData("move_from", KeyNotificationType.MoveFrom)]
    [InlineData("move_to", KeyNotificationType.MoveTo)]
    [InlineData("persist", KeyNotificationType.Persist)]
    [InlineData("rename_from", KeyNotificationType.RenameFrom)]
    [InlineData("rename_to", KeyNotificationType.RenameTo)]
    [InlineData("restore", KeyNotificationType.Restore)]
    [InlineData("rpop", KeyNotificationType.RPop)]
    [InlineData("rpush", KeyNotificationType.RPush)]
    [InlineData("sadd", KeyNotificationType.SAdd)]
    [InlineData("set", KeyNotificationType.Set)]
    [InlineData("setrange", KeyNotificationType.SetRange)]
    [InlineData("sortstore", KeyNotificationType.SortStore)]
    [InlineData("srem", KeyNotificationType.SRem)]
    [InlineData("spop", KeyNotificationType.SPop)]
    [InlineData("xadd", KeyNotificationType.XAdd)]
    [InlineData("xdel", KeyNotificationType.XDel)]
    [InlineData("xgroup-createconsumer", KeyNotificationType.XGroupCreateConsumer)]
    [InlineData("xgroup-create", KeyNotificationType.XGroupCreate)]
    [InlineData("xgroup-delconsumer", KeyNotificationType.XGroupDelConsumer)]
    [InlineData("xgroup-destroy", KeyNotificationType.XGroupDestroy)]
    [InlineData("xgroup-setid", KeyNotificationType.XGroupSetId)]
    [InlineData("xsetid", KeyNotificationType.XSetId)]
    [InlineData("xtrim", KeyNotificationType.XTrim)]
    [InlineData("zadd", KeyNotificationType.ZAdd)]
    [InlineData("zdiffstore", KeyNotificationType.ZDiffStore)]
    [InlineData("zinterstore", KeyNotificationType.ZInterStore)]
    [InlineData("zunionstore", KeyNotificationType.ZUnionStore)]
    [InlineData("zincr", KeyNotificationType.ZIncr)]
    [InlineData("zrembyrank", KeyNotificationType.ZRemByRank)]
    [InlineData("zrembyscore", KeyNotificationType.ZRemByScore)]
    [InlineData("zrem", KeyNotificationType.ZRem)]
    [InlineData("hexpire", KeyNotificationType.HExpire)]
    [InlineData("expired", KeyNotificationType.Expired)]
    [InlineData("evicted", KeyNotificationType.Evicted)]
    [InlineData("new", KeyNotificationType.New)]
    [InlineData("overwritten", KeyNotificationType.Overwritten)]
    [InlineData("type_changed", KeyNotificationType.TypeChanged)]
    public unsafe void ascii_hash_parse_all_known_values_parse_correctly(string raw, KeyNotificationType parsed)
    {
        var arr = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(raw.Length));
        int bytes;
        fixed (byte* bPtr = arr) // encode into the buffer
        {
            fixed (char* cPtr = raw)
            {
                bytes = Encoding.UTF8.GetBytes(cPtr, raw.Length, bPtr, arr.Length);
            }
        }

        var result = KeyNotificationTypeMetadata.Parse(arr.AsSpan(0, bytes));
        log.WriteLine($"Parsed '{raw}' as {result}");
        result.Should().Be(parsed);

        // and the other direction:
        var fetchedBytes = KeyNotificationTypeMetadata.GetRawBytes(parsed);
        string fetched;
        fixed (byte* bPtr = fetchedBytes)
        {
            fetched = Encoding.UTF8.GetString(bPtr, fetchedBytes.Length);
        }

        log.WriteLine($"Fetched '{raw}'");
        fetched.Should().Be(raw);

        ArrayPool<byte>.Shared.Return(arr);
    }

    [Fact]
    public void create_key_space_notification_valid()
    {
        var channel = RedisChannel.KeySpaceSingleKey("abc", 42);
        channel.ToString().Should().Be("__keyspace@42__:abc");
        channel.IsMultiNode.Should().BeFalse();
        channel.IsKeyRouted.Should().BeTrue();
        channel.IsSharded.Should().BeFalse();
        channel.IsPattern.Should().BeFalse();
        channel.IgnoreChannelPrefix.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, null, "__keyspace@*__:*")]
    [InlineData("abc*", null, "__keyspace@*__:abc*")]
    [InlineData(null, 42, "__keyspace@42__:*")]
    [InlineData("abc*", 42, "__keyspace@42__:abc*")]
    public void create_key_space_notification_pattern(string? pattern, int? database, string expected)
    {
        var channel = RedisChannel.KeySpacePattern(pattern, database);
        channel.ToString().Should().Be(expected);
        channel.IsMultiNode.Should().BeTrue();
        channel.IsKeyRouted.Should().BeFalse();
        channel.IsSharded.Should().BeFalse();
        channel.IsPattern.Should().BeTrue();
        channel.IgnoreChannelPrefix.Should().BeTrue();
    }

    [Theory]
    [InlineData("abc", null, "__keyspace@*__:abc*")]
    [InlineData("abc", 42, "__keyspace@42__:abc*")]
    public void create_key_space_notification_prefix_key(string prefix, int? database, string expected)
    {
        var channel = RedisChannel.KeySpacePrefix((RedisKey)prefix, database);
        channel.ToString().Should().Be(expected);
        channel.IsMultiNode.Should().BeTrue();
        channel.IsKeyRouted.Should().BeFalse();
        channel.IsSharded.Should().BeFalse();
        channel.IsPattern.Should().BeTrue();
        channel.IgnoreChannelPrefix.Should().BeTrue();
    }

    [Theory]
    [InlineData("abc", null, "__keyspace@*__:abc*")]
    [InlineData("abc", 42, "__keyspace@42__:abc*")]
    public void create_key_space_notification_prefix_span(string prefix, int? database, string expected)
    {
        var channel = RedisChannel.KeySpacePrefix((ReadOnlySpan<byte>)Encoding.UTF8.GetBytes(prefix), database);
        channel.ToString().Should().Be(expected);
        channel.IsMultiNode.Should().BeTrue();
        channel.IsKeyRouted.Should().BeFalse();
        channel.IsSharded.Should().BeFalse();
        channel.IsPattern.Should().BeTrue();
        channel.IgnoreChannelPrefix.Should().BeTrue();
    }

    [Theory]
    [InlineData("a?bc", null)]
    [InlineData("a?bc", 42)]
    [InlineData("a*bc", null)]
    [InlineData("a*bc", 42)]
    [InlineData("a[bc", null)]
    [InlineData("a[bc", 42)]
    public void create_key_space_notification_prefix_disallow_glob(string prefix, int? database)
    {
        var bytes = Encoding.UTF8.GetBytes(prefix);
        var ex = Assert.Throws<ArgumentException>(() =>
            RedisChannel.KeySpacePrefix((RedisKey)bytes, database));
        ex.Message.Should().StartWith("The supplied key contains pattern characters, but patterns are not supported in this context.");

        ex = Assert.Throws<ArgumentException>(() =>
            RedisChannel.KeySpacePrefix((ReadOnlySpan<byte>)bytes, database));
        ex.Message.Should().StartWith("The supplied key contains pattern characters, but patterns are not supported in this context.");
    }

    [Theory]
    [InlineData(KeyNotificationType.Set, null, "__keyevent@*__:set", true)]
    [InlineData(KeyNotificationType.XGroupCreate, null, "__keyevent@*__:xgroup-create", true)]
    [InlineData(KeyNotificationType.Set, 42, "__keyevent@42__:set", false)]
    [InlineData(KeyNotificationType.XGroupCreate, 42, "__keyevent@42__:xgroup-create", false)]
    public void create_key_event_notification(KeyNotificationType type, int? database, string expected, bool isPattern)
    {
        var channel = RedisChannel.KeyEvent(type, database);
        channel.ToString().Should().Be(expected);
        channel.IsMultiNode.Should().BeTrue();
        channel.IsKeyRouted.Should().BeFalse();
        channel.IsSharded.Should().BeFalse();
        channel.IgnoreChannelPrefix.Should().BeTrue();
        if (isPattern)
        {
            channel.IsPattern.Should().BeTrue();
        }
        else
        {
            channel.IsPattern.Should().BeFalse();
        }
    }

    [Fact]
    public void create_sub_key_space_notification_valid()
    {
        var channel = RedisChannel.SubKeySpaceSingleKey("myhash", 42);
        channel.ToString().Should().Be("__subkeyspace@42__:myhash");
        channel.IsMultiNode.Should().BeFalse();
        channel.IsKeyRouted.Should().BeTrue();
        channel.IsSharded.Should().BeFalse();
        channel.IsPattern.Should().BeFalse();
        channel.IgnoreChannelPrefix.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, null, "__subkeyspace@*__:*")]
    [InlineData("hash*", null, "__subkeyspace@*__:hash*")]
    [InlineData(null, 42, "__subkeyspace@42__:*")]
    [InlineData("hash*", 42, "__subkeyspace@42__:hash*")]
    public void create_sub_key_space_notification_pattern(string? pattern, int? database, string expected)
    {
        var channel = RedisChannel.SubKeySpacePattern(pattern, database);
        channel.ToString().Should().Be(expected);
        channel.IsMultiNode.Should().BeTrue();
        channel.IsKeyRouted.Should().BeFalse();
        channel.IsSharded.Should().BeFalse();
        channel.IsPattern.Should().BeTrue();
        channel.IgnoreChannelPrefix.Should().BeTrue();
    }

    [Theory]
    [InlineData("hash:", null, "__subkeyspace@*__:hash:*")]
    [InlineData("hash:", 42, "__subkeyspace@42__:hash:*")]
    public void create_sub_key_space_notification_prefix_key(string prefix, int? database, string expected)
    {
        var channel = RedisChannel.SubKeySpacePrefix((RedisKey)prefix, database);
        channel.ToString().Should().Be(expected);
        channel.IsMultiNode.Should().BeTrue();
        channel.IsKeyRouted.Should().BeFalse();
        channel.IsSharded.Should().BeFalse();
        channel.IsPattern.Should().BeTrue();
        channel.IgnoreChannelPrefix.Should().BeTrue();
    }

    [Theory]
    [InlineData("hash:", null, "__subkeyspace@*__:hash:*")]
    [InlineData("hash:", 42, "__subkeyspace@42__:hash:*")]
    public void create_sub_key_space_notification_prefix_span(string prefix, int? database, string expected)
    {
        var channel = RedisChannel.SubKeySpacePrefix((ReadOnlySpan<byte>)Encoding.UTF8.GetBytes(prefix), database);
        channel.ToString().Should().Be(expected);
        channel.IsMultiNode.Should().BeTrue();
        channel.IsKeyRouted.Should().BeFalse();
        channel.IsSharded.Should().BeFalse();
        channel.IsPattern.Should().BeTrue();
        channel.IgnoreChannelPrefix.Should().BeTrue();
    }

    [Theory]
    [InlineData("hash?", null)]
    [InlineData("hash?", 42)]
    [InlineData("hash*", null)]
    [InlineData("hash*", 42)]
    [InlineData("hash[", null)]
    [InlineData("hash[", 42)]
    public void create_sub_key_space_notification_prefix_disallow_glob(string prefix, int? database)
    {
        var bytes = Encoding.UTF8.GetBytes(prefix);
        var ex = Assert.Throws<ArgumentException>(() =>
            RedisChannel.SubKeySpacePrefix((RedisKey)bytes, database));
        ex.Message.Should().StartWith("The supplied key contains pattern characters, but patterns are not supported in this context.");

        ex = Assert.Throws<ArgumentException>(() =>
            RedisChannel.SubKeySpacePrefix((ReadOnlySpan<byte>)bytes, database));
        ex.Message.Should().StartWith("The supplied key contains pattern characters, but patterns are not supported in this context.");
    }

    [Theory]
    [InlineData(KeyNotificationType.HSet, null, "__subkeyevent@*__:hset", true)]
    [InlineData(KeyNotificationType.HDel, null, "__subkeyevent@*__:hdel", true)]
    [InlineData(KeyNotificationType.HSet, 42, "__subkeyevent@42__:hset", false)]
    [InlineData(KeyNotificationType.HDel, 42, "__subkeyevent@42__:hdel", false)]
    public void create_sub_key_event_notification(KeyNotificationType type, int? database, string expected, bool isPattern)
    {
        var channel = RedisChannel.SubKeyEvent(type, database);
        channel.ToString().Should().Be(expected);
        channel.IsMultiNode.Should().BeTrue();
        channel.IsKeyRouted.Should().BeFalse();
        channel.IsSharded.Should().BeFalse();
        channel.IgnoreChannelPrefix.Should().BeTrue();
        if (isPattern)
        {
            channel.IsPattern.Should().BeTrue();
        }
        else
        {
            channel.IsPattern.Should().BeFalse();
        }
    }

    [Fact]
    public void create_sub_key_space_item_notification_valid()
    {
        var channel = RedisChannel.SubKeySpaceItem("myhash", "field1", 42);
        channel.ToString().Should().Be("__subkeyspaceitem@42__:myhash\nfield1");
        channel.IsMultiNode.Should().BeFalse();
        channel.IsKeyRouted.Should().BeTrue();
        channel.IsSharded.Should().BeFalse();
        channel.IsPattern.Should().BeFalse();
        channel.IgnoreChannelPrefix.Should().BeTrue();
    }

    [Theory]
    [InlineData(KeyNotificationType.HSet, "myhash", null, "__subkeyspaceevent@*__:hset|myhash", true)]
    [InlineData(KeyNotificationType.HDel, "myhash", null, "__subkeyspaceevent@*__:hdel|myhash", true)]
    [InlineData(KeyNotificationType.HSet, "myhash", 42, "__subkeyspaceevent@42__:hset|myhash", false)]
    [InlineData(KeyNotificationType.HDel, "myhash", 42, "__subkeyspaceevent@42__:hdel|myhash", false)]
    public void create_sub_key_space_event_notification(KeyNotificationType type, string key, int? database, string expected, bool isPattern)
    {
        var channel = RedisChannel.SubKeySpaceEvent(type, key, database);
        channel.ToString().Should().Be(expected);
        channel.IsMultiNode.Should().BeTrue();
        channel.IsKeyRouted.Should().BeFalse();
        channel.IsSharded.Should().BeFalse();
        channel.IgnoreChannelPrefix.Should().BeTrue();
        if (isPattern)
        {
            channel.IsPattern.Should().BeTrue();
        }
        else
        {
            channel.IsPattern.Should().BeFalse();
        }
    }

    [Theory]
    [InlineData("abc", "__keyspace@42__:abc")]
    [InlineData("a*bc", "__keyspace@42__:a*bc")] // pattern-like is allowed, since not using PSUBSCRIBE
    public void cannot_key_route_key_space_single_key_is_key_routed(string key, string pattern)
    {
        var channel = RedisChannel.KeySpaceSingleKey(key, 42);
        channel.ToString().Should().Be(pattern);
        channel.IsMultiNode.Should().BeFalse();
        channel.IsPattern.Should().BeFalse();
        channel.IsSharded.Should().BeFalse();
        channel.IgnoreChannelPrefix.Should().BeTrue();
        channel.IsKeyRouted.Should().BeTrue();
        channel.WithKeyRouting().IsKeyRouted.Should().BeTrue(); // no change, still key-routed
        channel.GetPublishCommand().Should().Be(RedisCommand.PUBLISH);
    }

    [Fact]
    public void cannot_key_route_key_space_pattern()
    {
        var channel = RedisChannel.KeySpacePattern("abc", 42);
        channel.IsMultiNode.Should().BeTrue();
        channel.IsKeyRouted.Should().BeFalse();
        channel.IgnoreChannelPrefix.Should().BeTrue();
        Assert.Throws<InvalidOperationException>(() => channel.WithKeyRouting()).Message.Should().StartWith("Key routing is not supported for multi-node channels");
        Assert.Throws<InvalidOperationException>(() => channel.GetPublishCommand()).Message.Should().StartWith("Publishing is not supported for multi-node channels");
    }

    [Fact]
    public void cannot_key_route_key_event()
    {
        var channel = RedisChannel.KeyEvent(KeyNotificationType.Set, 42);
        channel.IsMultiNode.Should().BeTrue();
        channel.IsKeyRouted.Should().BeFalse();
        channel.IgnoreChannelPrefix.Should().BeTrue();
        Assert.Throws<InvalidOperationException>(() => channel.WithKeyRouting()).Message.Should().StartWith("Key routing is not supported for multi-node channels");
        Assert.Throws<InvalidOperationException>(() => channel.GetPublishCommand()).Message.Should().StartWith("Publishing is not supported for multi-node channels");
    }

    [Fact]
    public void cannot_key_route_key_event_custom()
    {
        var channel = RedisChannel.KeyEvent("foo"u8, 42);
        channel.IsMultiNode.Should().BeTrue();
        channel.IsKeyRouted.Should().BeFalse();
        channel.IgnoreChannelPrefix.Should().BeTrue();
        Assert.Throws<InvalidOperationException>(() => channel.WithKeyRouting()).Message.Should().StartWith("Key routing is not supported for multi-node channels");
        Assert.Throws<InvalidOperationException>(() => channel.GetPublishCommand()).Message.Should().StartWith("Publishing is not supported for multi-node channels");
    }

    [Fact]
    public void key_event_prefix_key_space_prefix_length_matches()
    {
        // this is a sanity check for the parsing step in KeyNotification.TryParse
        KeyNotificationChannels.KeyEventPrefix.Length.Should().Be(KeyNotificationChannels.KeySpacePrefix.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void key_notification_key_stripping(bool asString)
    {
        //Arrange
        Span<byte> blob = stackalloc byte[32];
        Span<char> clob = stackalloc char[32];
        RedisChannel channel = RedisChannel.Literal("__keyevent@0__:sadd");
        RedisValue value = asString ? "mykey:abc" : "mykey:abc"u8.ToArray();
        KeyNotification.TryParse(in channel, in value, out var notification);
        ((string?)notification.GetKey()).Should().Be("mykey:abc");
        notification.KeyStartsWith("mykey:"u8).Should().BeTrue();
        notification.KeyOffset.Should().Be(0);
        notification.GetKeyByteCount().Should().Be(9);
        notification.GetKeyMaxByteCount().Should().Be(asString ? 30 : 9);
        notification.GetKeyCharCount().Should().Be(9);
        notification.GetKeyMaxCharCount().Should().Be(asString ? 9 : 10);
        notification.TryCopyKey(blob, out var bytesWritten).Should().BeTrue();
        bytesWritten.Should().Be(9);
        Encoding.UTF8.GetString(blob.Slice(0, bytesWritten)).Should().Be("mykey:abc");
        notification.TryCopyKey(clob, out var charsWritten).Should().BeTrue();
        charsWritten.Should().Be(9);
        clob.Slice(0, charsWritten).ToString().Should().Be("mykey:abc");

        //Act
        // now with a prefix
        notification = notification.WithKeySlice("mykey:"u8.Length);

        //Assert
        ((string?)notification.GetKey()).Should().Be("abc");
        notification.KeyStartsWith("mykey:"u8).Should().BeFalse();
        notification.KeyOffset.Should().Be(6);
        notification.GetKeyByteCount().Should().Be(3);
        notification.GetKeyMaxByteCount().Should().Be(asString ? 24 : 3);
        notification.GetKeyCharCount().Should().Be(3);
        notification.GetKeyMaxCharCount().Should().Be(asString ? 3 : 4);
        notification.TryCopyKey(blob, out bytesWritten).Should().BeTrue();
        bytesWritten.Should().Be(3);
        Encoding.UTF8.GetString(blob.Slice(0, bytesWritten)).Should().Be("abc");
        notification.TryCopyKey(clob, out charsWritten).Should().BeTrue();
        charsWritten.Should().Be(3);
        clob.Slice(0, charsWritten).ToString().Should().Be("abc");
    }

    [Theory]
    [InlineData("hset|6:field1", "field1", "Single subkey")]
    [InlineData("hset|6:field1|6:field2", "field1", "Multiple subkeys - returns first only")]
    [InlineData("hset|6:field1|6:field2|6:field3", "field1", "Three subkeys - returns first only")]
    public void sub_key_space_h_set_parses_correctly(string payload, string expectedFirstSubKey, string description)
    {
        //Arrange
        // __subkeyspace@4__:mykey with payload like hset|6:field1 or hset|6:field1|6:field2
        var channel = RedisChannel.Literal("__subkeyspace@4__:mykey");

        //Act
        RedisValue value = payload;

        //Assert
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue(description);
        notification.Kind.Should().Be(KeyNotificationKind.SubKeySpace);
        notification.Database.Should().Be(4);
        notification.Type.Should().Be(KeyNotificationType.HSet);
        notification.IsType("hset"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("mykey");
        ((string?)notification.GetSubKeys().First()).Should().Be(expectedFirstSubKey);
    }

    [Theory]
    [InlineData("hset|6:field1", new[] { "field1" })]
    [InlineData("hset|6:field1|6:field2", new[] { "field1", "field2" })]
    [InlineData("hset|6:field1|6:field2|6:field3", new[] { "field1", "field2", "field3" })]
    [InlineData("hset|4:key1|5:key22|6:key333", new[] { "key1", "key22", "key333" })]
    public void sub_key_space_get_sub_keys(string payload, string[] expectedSubKeys)
    {
        var channel = RedisChannel.Literal("__subkeyspace@4__:mykey");
        RedisValue value = payload;

        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();

        var subKeys = new List<string?>();
        foreach (var subKey in notification.GetSubKeys())
        {
            subKeys.Add((string?)subKey);
        }

        subKeys.Should().Equal(expectedSubKeys);
    }

    [Theory]
    [InlineData("5:mykey|6:field1", "field1", "Single subkey")]
    [InlineData("5:mykey|6:field1|6:field2", "field1", "Multiple subkeys - returns first only")]
    [InlineData("5:mykey|6:field1|6:field2|6:field3", "field1", "Three subkeys - returns first only")]
    public void sub_key_event_h_set_parses_correctly(string payload, string expectedFirstSubKey, string description)
    {
        //Arrange
        // __subkeyevent@4__:hset with payload like 5:mykey|6:field1 or 5:mykey|6:field1|6:field2
        var channel = RedisChannel.Literal("__subkeyevent@4__:hset");

        //Act
        RedisValue value = payload;

        //Assert
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue(description);
        notification.Kind.Should().Be(KeyNotificationKind.SubKeyEvent);
        notification.Database.Should().Be(4);
        notification.Type.Should().Be(KeyNotificationType.HSet);
        notification.IsType("hset"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("mykey");
        ((string?)notification.GetSubKeys().First()).Should().Be(expectedFirstSubKey);
    }

    [Theory]
    [InlineData("5:mykey|6:field1", new[] { "field1" })]
    [InlineData("5:mykey|6:field1|6:field2", new[] { "field1", "field2" })]
    [InlineData("5:mykey|6:field1|6:field2|6:field3", new[] { "field1", "field2", "field3" })]
    public void sub_key_event_get_sub_keys(string payload, string[] expectedSubKeys)
    {
        var channel = RedisChannel.Literal("__subkeyevent@4__:hset");
        RedisValue value = payload;

        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();

        var subKeys = new List<string?>();
        foreach (var subKey in notification.GetSubKeys())
        {
            subKeys.Add((string?)subKey);
        }

        subKeys.Should().Equal(expectedSubKeys);
    }

    [Fact]
    public void sub_key_space_item_h_set_parses_correctly()
    {
        //Arrange
        // __subkeyspaceitem@4__:mykey\nfield1 with payload hset
        var channel = RedisChannel.Literal("__subkeyspaceitem@4__:mykey\nfield1");

        //Act
        RedisValue value = "hset";

        //Assert
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.SubKeySpaceItem);
        notification.Database.Should().Be(4);
        notification.Type.Should().Be(KeyNotificationType.HSet);
        notification.IsType("hset"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("mykey");
        ((string?)notification.GetSubKeys().First()).Should().Be("field1");
    }

    [Theory]
    [InlineData("6:field1", "field1", "Single subkey")]
    [InlineData("6:field1|6:field2", "field1", "Multiple subkeys - returns first only")]
    [InlineData("6:field1|6:field2|6:field3", "field1", "Three subkeys - returns first only")]
    public void sub_key_space_event_h_set_parses_correctly(string payload, string expectedFirstSubKey, string description)
    {
        //Arrange
        // __subkeyspaceevent@4__:hset|mykey with payload like 6:field1 or 6:field1|6:field2
        var channel = RedisChannel.Literal("__subkeyspaceevent@4__:hset|mykey");

        //Act
        RedisValue value = payload;

        //Assert
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue(description);
        notification.Kind.Should().Be(KeyNotificationKind.SubKeySpaceEvent);
        notification.Database.Should().Be(4);
        notification.Type.Should().Be(KeyNotificationType.HSet);
        notification.IsType("hset"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("mykey");
        ((string?)notification.GetSubKeys().First()).Should().Be(expectedFirstSubKey);
    }

    [Theory]
    [InlineData("6:field1", new[] { "field1" })]
    [InlineData("6:field1|6:field2", new[] { "field1", "field2" })]
    [InlineData("6:field1|6:field2|6:field3", new[] { "field1", "field2", "field3" })]
    public void sub_key_space_event_get_sub_keys(string payload, string[] expectedSubKeys)
    {
        var channel = RedisChannel.Literal("__subkeyspaceevent@4__:hset|mykey");
        RedisValue value = payload;

        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();

        var subKeys = new List<string?>();
        foreach (var subKey in notification.GetSubKeys())
        {
            subKeys.Add((string?)subKey);
        }

        subKeys.Should().Equal(expectedSubKeys);
    }

    [Fact]
    public void sub_key_space_item_get_single_sub_key()
    {
        // __subkeyspaceitem@4__:mykey\nfield1
        var channel = RedisChannel.Literal("__subkeyspaceitem@4__:mykey\nfield1");
        RedisValue value = RedisValue.EmptyString;

        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();

        var subKeys = new List<string?>();
        foreach (var subKey in notification.GetSubKeys())
        {
            subKeys.Add((string?)subKey);
        }

        subKeys.Should().ContainSingle();
        subKeys[0].Should().Be("field1");
    }

    [Fact]
    public void get_sub_keys_default_enumerable_returns_empty()
    {
        // Test that default SubKeyEnumerable returns empty set
        var enumerable = default(KeyNotification.SubKeyEnumerable);

        var subKeys = new List<string?>();
        foreach (var subKey in enumerable)
        {
            subKeys.Add((string?)subKey);
        }

        subKeys.Should().BeEmpty();
    }

    [Fact]
    public void get_sub_keys_default_enumerator_move_next_returns_false()
    {
        // Test that default SubKeyEnumerator's MoveNext returns false
        var enumerator = default(KeyNotification.SubKeyEnumerator);

        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void get_sub_keys_non_sub_key_notification_returns_empty()
    {
        // Regular keyspace notification (not sub-key) should return empty
        var channel = RedisChannel.Literal("__keyspace@4__:mykey");
        RedisValue value = "set";

        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeySpace);

        var subKeys = new List<string?>();
        foreach (var subKey in notification.GetSubKeys())
        {
            subKeys.Add((string?)subKey);
        }

        subKeys.Should().BeEmpty();
    }

    [Fact]
    public void get_sub_keys_count_returns_correct_count()
    {
        //Arrange
        var channel = RedisChannel.Literal("__subkeyspace@4__:mykey");

        //Act
        RedisValue value = "hset|6:field1|6:field2|6:field3";

        //Assert
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();
        notification.GetSubKeys().Count().Should().Be(3);
    }

    [Fact]
    public void get_sub_keys_first_returns_first_element()
    {
        //Arrange
        var channel = RedisChannel.Literal("__subkeyspace@4__:mykey");

        //Act
        RedisValue value = "hset|6:field1|6:field2|6:field3";

        //Assert
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();
        ((string?)notification.GetSubKeys().First()).Should().Be("field1");
    }

    [Fact]
    public void get_sub_keys_first_throws_on_empty()
    {
        var channel = RedisChannel.Literal("__keyspace@4__:mykey");
        RedisValue value = "set";

        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();

        try
        {
            notification.GetSubKeys().First();
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }
    }

    [Fact]
    public void get_sub_keys_first_or_default_returns_first_element()
    {
        //Arrange
        var channel = RedisChannel.Literal("__subkeyspace@4__:mykey");

        //Act
        RedisValue value = "hset|6:field1|6:field2|6:field3";

        //Assert
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();
        ((string?)notification.GetSubKeys().FirstOrDefault()).Should().Be("field1");
    }

    [Fact]
    public void get_sub_keys_first_or_default_returns_null_on_empty()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyspace@4__:mykey");

        //Act
        RedisValue value = "set";

        //Assert
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();
        notification.GetSubKeys().FirstOrDefault().IsNull.Should().BeTrue();
    }

    [Fact]
    public void get_sub_keys_copy_to_copies_all_elements()
    {
        //Arrange
        var channel = RedisChannel.Literal("__subkeyspace@4__:mykey");
        RedisValue value = "hset|6:field1|6:field2|6:field3";
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();
        var destination = new RedisValue[5];

        //Act
        var count = notification.GetSubKeys().CopyTo(destination);

        //Assert
        count.Should().Be(3);
        ((string?)destination[0]).Should().Be("field1");
        ((string?)destination[1]).Should().Be("field2");
        ((string?)destination[2]).Should().Be("field3");
    }

    [Fact]
    public void get_sub_keys_copy_to_truncates_when_too_small()
    {
        //Arrange
        var channel = RedisChannel.Literal("__subkeyspace@4__:mykey");
        RedisValue value = "hset|6:field1|6:field2|6:field3";
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();
        var destination = new RedisValue[2];

        //Act
        var count = notification.GetSubKeys().CopyTo(destination);

        //Assert
        count.Should().Be(2);
        ((string?)destination[0]).Should().Be("field1");
        ((string?)destination[1]).Should().Be("field2");
    }

    [Fact]
    public void get_sub_keys_to_array_returns_array()
    {
        //Arrange
        var channel = RedisChannel.Literal("__subkeyspace@4__:mykey");
        RedisValue value = "hset|6:field1|6:field2|6:field3";
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();

        //Act
        var array = notification.GetSubKeys().ToArray();

        //Assert
        array.Length.Should().Be(3);
        ((string?)array[0]).Should().Be("field1");
        ((string?)array[1]).Should().Be("field2");
        ((string?)array[2]).Should().Be("field3");
    }

    [Fact]
    public void get_sub_keys_to_list_returns_list()
    {
        //Arrange
        var channel = RedisChannel.Literal("__subkeyspace@4__:mykey");
        RedisValue value = "hset|6:field1|6:field2|6:field3";
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();

        //Act
        var list = notification.GetSubKeys().ToList();

        //Assert
        list.Count.Should().Be(3);
        ((string?)list[0]).Should().Be("field1");
        ((string?)list[1]).Should().Be("field2");
        ((string?)list[2]).Should().Be("field3");
    }

    [Fact]
    public void get_sub_keys_enumerator_current_span_and_copy_works_without_current()
    {
        var channel = RedisChannel.Literal("__subkeyspace@4__:mykey");
        RedisValue value = "hset|6:field1|6:field2";

        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();

        using var enumerator = notification.GetSubKeys().GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();

        enumerator.CurrentByteCount.Should().Be(6);
        enumerator.CurrentSpan.ToArray().Should().Equal("field1"u8.ToArray());
        enumerator.GetCurrentCharCount().Should().Be(6);

        Span<byte> byteBuffer = stackalloc byte[16];
        enumerator.TryCopyTo(byteBuffer, out var bytesWritten).Should().BeTrue();
        bytesWritten.Should().Be(6);
        Encoding.UTF8.GetString(byteBuffer.Slice(0, bytesWritten)).Should().Be("field1");

        Span<char> charBuffer = stackalloc char[16];
        enumerator.TryCopyTo(charBuffer, out var charsWritten).Should().BeTrue();
        charsWritten.Should().Be(6);
        charBuffer.Slice(0, charsWritten).ToString().Should().Be("field1");

        enumerator.MoveNext().Should().BeTrue();
        enumerator.CurrentSpan.ToArray().Should().Equal("field2"u8.ToArray());
    }

    [Fact]
    public void get_sub_keys_enumerator_current_survives_dispose()
    {
        var channel = RedisChannel.Literal("__subkeyspaceevent@4__:hset|mykey");
        RedisValue value = "6:field1|6:field2";

        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();

        RedisValue first;
        using (var enumerator = notification.GetSubKeys().GetEnumerator())
        {
            enumerator.MoveNext().Should().BeTrue();
            first = enumerator.Current;
            ((string?)enumerator.Current).Should().Be("field1");
        }

        ((string?)first).Should().Be("field1");
    }

    [Fact]
    public void extract_length_prefixed_value_parses_correctly()
    {
        //Arrange
        // Test the length-prefixed value extraction helper
        var result1 = KeyNotification.ExtractLengthPrefixedValue("6:field1"u8);
        ((string?)result1).Should().Be("field1");
        var result2 = KeyNotification.ExtractLengthPrefixedValue("5:mykey"u8);
        ((string?)result2).Should().Be("mykey");
        var result3 = KeyNotification.ExtractLengthPrefixedValue("11:hello world"u8);
        ((string?)result3).Should().Be("hello world");
        // Test invalid formats
        var result4 = KeyNotification.ExtractLengthPrefixedValue("invalid"u8);
        result4.IsNull.Should().BeTrue();

        //Act
        var result5 = KeyNotification.ExtractLengthPrefixedValue("10:short"u8);

        //Assert
        // Length mismatch
        result5.IsNull.Should().BeTrue();
    }

    [Fact]
    public void sub_key_space_get_sub_key_returns_correct_value()
    {
        //Arrange
        // Test that GetSubKey returns the expected value for SubKeySpace
        var channel = RedisChannel.Literal("__subkeyspace@4__:mykey");
        RedisValue value = "hset|6:field1";
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.SubKeySpace);

        //Act
        var subKey = notification.GetSubKeys().First();

        //Assert
        subKey.IsNull.Should().BeFalse($"SubKey should not be null. Value: {value}");
        ((string?)subKey).Should().Be("field1");
    }

    [Fact]
    public void channel_suffix_sub_key_event_returns_correct_value()
    {
        //Arrange
        // Test that ChannelSuffix returns the expected value for SubKeyEvent
        var channel = RedisChannel.Literal("__subkeyevent@4__:hset");
        RedisValue value = "5:mykey|6:field1";
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.SubKeyEvent);
        var suffix = notification.ChannelSuffix;

        //Act
        var expected = "hset"u8;

        //Assert
        suffix.Length.Should().Be(expected.Length);
        suffix.SequenceEqual(expected).Should().BeTrue("ChannelSuffix should equal 'hset'");
    }

    [Fact]
    public void sub_key_space_h_expire_parses_correctly()
    {
        //Arrange
        // __subkeyspace@0__:hash with payload hexpire|5:field
        var channel = RedisChannel.Literal("__subkeyspace@0__:hash");

        //Act
        RedisValue value = "hexpire|5:field";

        //Assert
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.SubKeySpace);
        notification.Database.Should().Be(0);
        notification.Type.Should().Be(KeyNotificationType.HExpire);
        notification.IsType("hexpire"u8).Should().BeTrue();
        ((string?)notification.GetKey()).Should().Be("hash");
        ((string?)notification.GetSubKeys().First()).Should().Be("field");
    }

    [Fact]
    public void non_sub_key_notifications_return_null_sub_key()
    {
        //Arrange
        // Regular keyspace notification
        var channel = RedisChannel.Literal("__keyspace@4__:mykey");
        RedisValue value = "set";
        KeyNotification.TryParse(channel, value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeySpace);
        notification.GetSubKeys().FirstOrDefault().IsNull.Should().BeTrue();
        // Regular keyevent notification
        channel = RedisChannel.Literal("__keyevent@4__:del");

        //Act
        value = "mykey";

        //Assert
        KeyNotification.TryParse(channel, value, out notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeyEvent);
        notification.GetSubKeys().FirstOrDefault().IsNull.Should().BeTrue();
    }

    [Fact]
    public void key_prefix_key_space_matching_prefix_parses_and_strips()
    {
        //Arrange
        // __keyspace@1__:foo:bar with payload "set"
        // Key prefix is "foo:"
        var channel = RedisChannel.Literal("__keyspace@1__:foo:bar");
        RedisValue value = "set";

        //Act
        ReadOnlySpan<byte> keyPrefix = "foo:"u8;

        //Assert
        KeyNotification.TryParse(keyPrefix, in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeySpace);
        notification.Database.Should().Be(1);
        notification.Type.Should().Be(KeyNotificationType.Set);
        // The key should NOT include the prefix
        ((string?)notification.GetKey()).Should().Be("bar");
        notification.GetKeyByteCount().Should().Be(3);
        notification.GetKeyCharCount().Should().Be(3);
    }

    [Fact]
    public void key_prefix_key_space_non_matching_prefix_returns_false()
    {
        //Arrange
        // __keyspace@1__:other:bar with payload "set"
        // Key prefix is "foo:"
        var channel = RedisChannel.Literal("__keyspace@1__:other:bar");
        RedisValue value = "set";

        //Act
        ReadOnlySpan<byte> keyPrefix = "foo:"u8;

        //Assert
        // Should return false because the key doesn't start with "foo:"
        KeyNotification.TryParse(keyPrefix, in channel, in value, out var notification).Should().BeFalse();
    }

    [Fact]
    public void key_prefix_key_event_matching_prefix_parses_and_strips()
    {
        //Arrange
        // __keyevent@1__:set with payload "foo:bar"
        // Key prefix is "foo:"
        var channel = RedisChannel.Literal("__keyevent@1__:set");
        RedisValue value = "foo:bar";

        //Act
        ReadOnlySpan<byte> keyPrefix = "foo:"u8;

        //Assert
        KeyNotification.TryParse(keyPrefix, in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeyEvent);
        notification.Database.Should().Be(1);
        notification.Type.Should().Be(KeyNotificationType.Set);
        // The key should NOT include the prefix
        ((string?)notification.GetKey()).Should().Be("bar");
        notification.GetKeyByteCount().Should().Be(3);
        notification.GetKeyCharCount().Should().Be(3);
    }

    [Fact]
    public void key_prefix_key_event_non_matching_prefix_returns_false()
    {
        //Arrange
        // __keyevent@1__:set with payload "other:bar"
        // Key prefix is "foo:"
        var channel = RedisChannel.Literal("__keyevent@1__:set");
        RedisValue value = "other:bar";

        //Act
        ReadOnlySpan<byte> keyPrefix = "foo:"u8;

        //Assert
        // Should return false because the key doesn't start with "foo:"
        KeyNotification.TryParse(keyPrefix, in channel, in value, out var notification).Should().BeFalse();
    }

    [Fact]
    public void key_prefix_key_space_empty_prefix_parses_without_stripping()
    {
        //Arrange
        // __keyspace@1__:mykey with payload "set"
        // Empty prefix
        var channel = RedisChannel.Literal("__keyspace@1__:mykey");
        RedisValue value = "set";

        //Act
        ReadOnlySpan<byte> keyPrefix = ""u8;

        //Assert
        KeyNotification.TryParse(keyPrefix, in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeySpace);
        // The key should be unchanged
        ((string?)notification.GetKey()).Should().Be("mykey");
        notification.GetKeyByteCount().Should().Be(5);
    }

    [Fact]
    public void key_prefix_key_space_prefix_longer_than_key_returns_false()
    {
        //Arrange
        // __keyspace@1__:foo with payload "set"
        // Key prefix is "foo:bar" which is longer than the actual key
        var channel = RedisChannel.Literal("__keyspace@1__:foo");
        RedisValue value = "set";

        //Act
        ReadOnlySpan<byte> keyPrefix = "foo:bar"u8;

        //Assert
        // Should return false because prefix is longer than the key
        KeyNotification.TryParse(keyPrefix, in channel, in value, out var notification).Should().BeFalse();
    }

    [Fact]
    public void key_prefix_key_space_exact_match_returns_empty_key()
    {
        //Arrange
        // __keyspace@1__:foo with payload "set"
        // Key prefix is exactly "foo"
        var channel = RedisChannel.Literal("__keyspace@1__:foo");
        RedisValue value = "set";

        //Act
        ReadOnlySpan<byte> keyPrefix = "foo"u8;

        //Assert
        KeyNotification.TryParse(keyPrefix, in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.KeySpace);
        // The key should be empty after stripping the prefix
        ((string?)notification.GetKey()).Should().Be("");
        notification.GetKeyByteCount().Should().Be(0);
        notification.GetKeyCharCount().Should().Be(0);
    }

    [Fact]
    public void key_prefix_multi_tenant_scenario_isolates_correctly()
    {
        //Arrange
        // Simulate a multi-tenant scenario with client prefixes
        ReadOnlySpan<byte> client1Prefix = "client1234:"u8;
        ReadOnlySpan<byte> client5678Prefix = "client5678:"u8;
        // Client 1's notification
        var channel1 = RedisChannel.Literal("__keyspace@0__:client1234:order/123");
        RedisValue value1 = "set";
        // Client 2's notification (different client)
        var channel2 = RedisChannel.Literal("__keyspace@0__:client5678:order/456");

        //Act
        RedisValue value2 = "set";

        //Assert
        // Client 1 should only see their own notifications
        KeyNotification.TryParse(client1Prefix, in channel1, in value1, out var notification1).Should().BeTrue();
        ((string?)notification1.GetKey()).Should().Be("order/123");
        KeyNotification.TryParse(client1Prefix, in channel2, in value2, out _).Should().BeFalse();
        // Client 2 should only see their own notifications
        KeyNotification.TryParse(client5678Prefix, in channel2, in value2, out var notification2).Should().BeTrue();
        ((string?)notification2.GetKey()).Should().Be("order/456");
        KeyNotification.TryParse(client5678Prefix, in channel1, in value1, out _).Should().BeFalse();
    }

    [Fact]
    public void try_copy_key_key_space_works()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyspace@1__:testkey");
        RedisValue value = "set";
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        // Test byte copy
        Span<byte> byteBuffer = stackalloc byte[20];
        notification.TryCopyKey(byteBuffer, out var bytesWritten).Should().BeTrue();
        bytesWritten.Should().Be(7);
        Encoding.UTF8.GetString(byteBuffer.Slice(0, bytesWritten)).Should().Be("testkey");

        //Act
        // Test char copy
        Span<char> charBuffer = stackalloc char[20];

        //Assert
        notification.TryCopyKey(charBuffer, out var charsWritten).Should().BeTrue();
        charsWritten.Should().Be(7);
        (new string(charBuffer.Slice(0, charsWritten).ToArray())).Should().Be("testkey");
    }

    [Fact]
    public void try_copy_key_key_event_works()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyevent@1__:set");
        RedisValue value = "testkey";
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        // Test byte copy
        Span<byte> byteBuffer = stackalloc byte[20];
        notification.TryCopyKey(byteBuffer, out var bytesWritten).Should().BeTrue();
        bytesWritten.Should().Be(7);
        Encoding.UTF8.GetString(byteBuffer.Slice(0, bytesWritten)).Should().Be("testkey");

        //Act
        // Test char copy
        Span<char> charBuffer = stackalloc char[20];

        //Assert
        notification.TryCopyKey(charBuffer, out var charsWritten).Should().BeTrue();
        charsWritten.Should().Be(7);
        (new string(charBuffer.Slice(0, charsWritten).ToArray())).Should().Be("testkey");
    }

    [Fact]
    public void try_copy_key_sub_key_space_works()
    {
        //Arrange
        var channel = RedisChannel.Literal("__subkeyspace@1__:mykey");
        RedisValue value = "hset|6:field1";
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        // Test byte copy
        Span<byte> byteBuffer = stackalloc byte[20];
        notification.TryCopyKey(byteBuffer, out var bytesWritten).Should().BeTrue();
        bytesWritten.Should().Be(5);
        Encoding.UTF8.GetString(byteBuffer.Slice(0, bytesWritten)).Should().Be("mykey");

        //Act
        // Test char copy
        Span<char> charBuffer = stackalloc char[20];

        //Assert
        notification.TryCopyKey(charBuffer, out var charsWritten).Should().BeTrue();
        charsWritten.Should().Be(5);
        (new string(charBuffer.Slice(0, charsWritten).ToArray())).Should().Be("mykey");
    }

    [Fact]
    public void try_copy_key_sub_key_event_works()
    {
        //Arrange
        var channel = RedisChannel.Literal("__subkeyevent@1__:hset");
        RedisValue value = "5:mykey|6:field1";
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        // Test byte copy
        Span<byte> byteBuffer = stackalloc byte[20];
        notification.TryCopyKey(byteBuffer, out var bytesWritten).Should().BeTrue();
        bytesWritten.Should().Be(5);
        Encoding.UTF8.GetString(byteBuffer.Slice(0, bytesWritten)).Should().Be("mykey");

        //Act
        // Test char copy
        Span<char> charBuffer = stackalloc char[20];

        //Assert
        notification.TryCopyKey(charBuffer, out var charsWritten).Should().BeTrue();
        charsWritten.Should().Be(5);
        (new string(charBuffer.Slice(0, charsWritten).ToArray())).Should().Be("mykey");
    }

    [Fact]
    public void try_copy_key_sub_key_space_item_works()
    {
        //Arrange
        var channel = RedisChannel.Literal("__subkeyspaceitem@1__:mykey\nfield1");
        RedisValue value = "hset";
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        // Test byte copy
        Span<byte> byteBuffer = stackalloc byte[20];
        notification.TryCopyKey(byteBuffer, out var bytesWritten).Should().BeTrue();
        bytesWritten.Should().Be(5);
        Encoding.UTF8.GetString(byteBuffer.Slice(0, bytesWritten)).Should().Be("mykey");

        //Act
        // Test char copy
        Span<char> charBuffer = stackalloc char[20];

        //Assert
        notification.TryCopyKey(charBuffer, out var charsWritten).Should().BeTrue();
        charsWritten.Should().Be(5);
        (new string(charBuffer.Slice(0, charsWritten).ToArray())).Should().Be("mykey");
    }

    [Fact]
    public void try_copy_key_sub_key_space_event_works()
    {
        //Arrange
        var channel = RedisChannel.Literal("__subkeyspaceevent@1__:hset|mykey");
        RedisValue value = "6:field1";
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        // Test byte copy
        Span<byte> byteBuffer = stackalloc byte[20];
        notification.TryCopyKey(byteBuffer, out var bytesWritten).Should().BeTrue();
        bytesWritten.Should().Be(5);
        Encoding.UTF8.GetString(byteBuffer.Slice(0, bytesWritten)).Should().Be("mykey");

        //Act
        // Test char copy
        Span<char> charBuffer = stackalloc char[20];

        //Assert
        notification.TryCopyKey(charBuffer, out var charsWritten).Should().BeTrue();
        charsWritten.Should().Be(5);
        (new string(charBuffer.Slice(0, charsWritten).ToArray())).Should().Be("mykey");
    }

    [Fact]
    public void try_copy_key_buffer_too_small_returns_false()
    {
        //Arrange
        var channel = RedisChannel.Literal("__keyspace@1__:verylongkeyname");
        RedisValue value = "set";
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        // Test with buffer that's too small
        Span<byte> tinyBuffer = stackalloc byte[5];
        notification.TryCopyKey(tinyBuffer, out var bytesWritten).Should().BeFalse();
        bytesWritten.Should().Be(15);

        //Act
        // Should report the actual size needed

        // Test char buffer too small
        Span<char> tinyCharBuffer = stackalloc char[5];

        //Assert
        notification.TryCopyKey(tinyCharBuffer, out var charsWritten).Should().BeFalse();
    }

    [Fact]
    public void sub_key_sub_key_space_subkey_not_affected_by_key_prefix()
    {
        //Arrange
        // Test that subkey contains its own prefix and is not affected by the key prefix
        var channel = RedisChannel.Literal("__subkeyspace@1__:user:123");
        RedisValue value = "hset|12:email:123456";
        // subkey has different prefix "email:"
        ReadOnlySpan<byte> keyPrefix = "user:"u8;
        // key prefix is "user:"

        KeyNotification.TryParse(keyPrefix, in channel, in value, out var notification).Should().BeTrue();
        // The key should have the "user:" prefix stripped
        ((string?)notification.GetKey()).Should().Be("123");

        //Act
        // The subkey should be returned as-is with its own "email:" prefix intact
        var subkey = notification.GetSubKeys().First();

        //Assert
        ((string?)subkey).Should().Be("email:123456");
        subkey.GetByteCount().Should().Be(12);
    }

    [Fact]
    public void sub_key_sub_key_event_subkey_not_affected_by_key_prefix()
    {
        //Arrange
        // Test that subkey is independent of key prefix
        var channel = RedisChannel.Literal("__subkeyevent@1__:hset");
        RedisValue value = "8:user:123|12:email:123456";
        // key has "user:" prefix, subkey has "email:" prefix
        ReadOnlySpan<byte> keyPrefix = "user:"u8;
        KeyNotification.TryParse(keyPrefix, in channel, in value, out var notification).Should().BeTrue();
        // The key should have the "user:" prefix stripped
        ((string?)notification.GetKey()).Should().Be("123");

        //Act
        // The subkey should be returned as-is with its own "email:" prefix intact
        var subkey = notification.GetSubKeys().First();

        //Assert
        ((string?)subkey).Should().Be("email:123456");
        subkey.GetByteCount().Should().Be(12);
    }

    [Fact]
    public void sub_key_sub_key_space_item_subkey_not_affected_by_key_prefix()
    {
        //Arrange
        // Test that subkey in channel is independent of key prefix
        var channel = RedisChannel.Literal("__subkeyspaceitem@1__:user:123\nemail:123456");
        RedisValue value = "hset";
        ReadOnlySpan<byte> keyPrefix = "user:"u8;
        KeyNotification.TryParse(keyPrefix, in channel, in value, out var notification).Should().BeTrue();
        // The key should have the "user:" prefix stripped
        ((string?)notification.GetKey()).Should().Be("123");

        //Act
        // The subkey should be returned as-is with its own "email:" prefix intact
        var subkey = notification.GetSubKeys().First();

        //Assert
        ((string?)subkey).Should().Be("email:123456");
    }

    [Fact]
    public void sub_key_sub_key_space_event_subkey_not_affected_by_key_prefix()
    {
        //Arrange
        // Test that subkey in payload is independent of key prefix
        var channel = RedisChannel.Literal("__subkeyspaceevent@1__:hset|user:123");
        RedisValue value = "12:email:123456";
        ReadOnlySpan<byte> keyPrefix = "user:"u8;
        KeyNotification.TryParse(keyPrefix, in channel, in value, out var notification).Should().BeTrue();
        // The key should have the "user:" prefix stripped
        ((string?)notification.GetKey()).Should().Be("123");

        //Act
        // The subkey should be returned as-is with its own "email:" prefix intact
        var subkey = notification.GetSubKeys().First();

        //Assert
        ((string?)subkey).Should().Be("email:123456");
        subkey.GetByteCount().Should().Be(12);
    }

    [Fact]
    public void sub_key_event_multiple_fields_parses_correctly()
    {
        //Arrange
        // __subkeyevent@0__:hset with payload "1:k|6:field1,6:field2,6:field3"
        // This represents an HSET on key "k" with fields "field1", "field2", "field3"
        // Note: fields are separated by commas, not pipes (as per Redis 8.8 actual behavior)
        var channel = RedisChannel.Literal("__subkeyevent@0__:hset");
        RedisValue value = "1:k|6:field1,6:field2,6:field3";
        log.WriteLine($"Testing channel: '{channel}', value: '{value}'");
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.SubKeyEvent);
        notification.Database.Should().Be(0);
        notification.Type.Should().Be(KeyNotificationType.HSet);
        ((string?)notification.GetKey()).Should().Be("k");
        // Test sub-keys
        var subKeys = notification.GetSubKeys();
        int count = subKeys.Count();
        log.WriteLine($"Sub-key count: {count}");
        count.Should().Be(3);

        //Act
        var fieldsList = subKeys.ToList();

        //Assert
        fieldsList.Count.Should().Be(3);
        ((string?)fieldsList[0]).Should().Be("field1");
        ((string?)fieldsList[1]).Should().Be("field2");
        ((string?)fieldsList[2]).Should().Be("field3");
    }

    [Fact]
    public void sub_key_event_real_world_payload_parses_correctly()
    {
        //Arrange
        // Real payload observed from Redis 8.8 server
        var channel = RedisChannel.Literal("__subkeyevent@0__:hset");
        RedisValue value = "41:d7213ec1-e834-4fb7-9a4d-a0d8d6bfbc7e/hash|6:field1,6:field2,6:field3";
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.SubKeyEvent);
        notification.Database.Should().Be(0);
        notification.Type.Should().Be(KeyNotificationType.HSet);
        ((string?)notification.GetKey()).Should().Be("d7213ec1-e834-4fb7-9a4d-a0d8d6bfbc7e/hash");
        // Test sub-keys
        var subKeys = notification.GetSubKeys();
        subKeys.Count().Should().Be(3);

        //Act
        var fieldsList = subKeys.ToArray();

        //Assert
        ((string?)fieldsList[0]).Should().Be("field1");
        ((string?)fieldsList[1]).Should().Be("field2");
        ((string?)fieldsList[2]).Should().Be("field3");
    }

    [Fact]
    public void sub_key_space_multiple_fields_parses_correctly()
    {
        //Arrange
        // __subkeyspace@0__:mykey with payload "hset|6:field1,6:field2"
        // Format: <event>|<len>:<subkey>,<len>:<subkey>...
        var channel = RedisChannel.Literal("__subkeyspace@0__:mykey");
        RedisValue value = "hset|6:field1,6:field2";
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.SubKeySpace);
        notification.Database.Should().Be(0);
        notification.Type.Should().Be(KeyNotificationType.HSet);
        ((string?)notification.GetKey()).Should().Be("mykey");
        var subKeys = notification.GetSubKeys();
        subKeys.Count().Should().Be(2);

        //Act
        var fieldsList = subKeys.ToArray();

        //Assert
        ((string?)fieldsList[0]).Should().Be("field1");
        ((string?)fieldsList[1]).Should().Be("field2");
    }

    [Fact]
    public void sub_key_space_event_multiple_fields_parses_correctly()
    {
        //Arrange
        // __subkeyspaceevent@0__:hset|mykey with payload "6:field1,6:field2,6:field3"
        var channel = RedisChannel.Literal("__subkeyspaceevent@0__:hset|mykey");
        RedisValue value = "6:field1,6:field2,6:field3";
        KeyNotification.TryParse(in channel, in value, out var notification).Should().BeTrue();
        notification.Kind.Should().Be(KeyNotificationKind.SubKeySpaceEvent);
        notification.Database.Should().Be(0);
        notification.Type.Should().Be(KeyNotificationType.HSet);
        ((string?)notification.GetKey()).Should().Be("mykey");
        var subKeys = notification.GetSubKeys();
        subKeys.Count().Should().Be(3);

        //Act
        var fieldsList = subKeys.ToArray();

        //Assert
        ((string?)fieldsList[0]).Should().Be("field1");
        ((string?)fieldsList[1]).Should().Be("field2");
        ((string?)fieldsList[2]).Should().Be("field3");
    }

    [Fact]
    public void get_raw_bytes_unknown_throws()
    {
        // Unknown is a client-side value with no wire token (an explicit empty AsciiHash), so
        // there is no channel name to emit: reaching here means something is already wrong
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = KeyNotificationTypeMetadata.GetRawBytes(KeyNotificationType.Unknown).Length;
        });
        ex.ParamName.Should().Be("type");

        // and the public channel factories that route through it
        Assert.Throws<ArgumentOutOfRangeException>(() => RedisChannel.KeyEvent(KeyNotificationType.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => RedisChannel.SubKeyEvent(KeyNotificationType.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => RedisChannel.SubKeySpaceEvent(KeyNotificationType.Unknown, "mykey"));
    }
}

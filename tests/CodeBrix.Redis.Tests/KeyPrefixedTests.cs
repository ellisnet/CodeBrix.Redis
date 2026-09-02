using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CodeBrix.Redis.KeyspaceIsolation;
using CodeBrix.TestMocks.Mocking;
using Xunit;
using static CodeBrix.Redis.Tests.KeyPrefixedDatabaseTests; // for IsKeys etc

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(nameof(SubstituteDependentCollection))]
public sealed class KeyPrefixedTests
{
    private readonly Mock<IDatabaseAsync> mock;
    private readonly KeyPrefixed<IDatabaseAsync> prefixed;

    public KeyPrefixedTests()
    {
        mock = new Mock<IDatabaseAsync>();
        prefixed = new KeyPrefixed<IDatabaseAsync>(mock.Object, Encoding.UTF8.GetBytes("prefix:"));
    }

    [Fact]
    public async Task debug_object_async()
    {
        await prefixed.DebugObjectAsync("key", CommandFlags.None);
        mock.Verify(x => x.DebugObjectAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_decrement_async_1()
    {
        await prefixed.HashDecrementAsync("key", "hashField", 123, CommandFlags.None);
        mock.Verify(x => x.HashDecrementAsync("prefix:key", "hashField", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_decrement_async_2()
    {
        await prefixed.HashDecrementAsync("key", "hashField", 1.23, CommandFlags.None);
        mock.Verify(x => x.HashDecrementAsync("prefix:key", "hashField", 1.23, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_delete_async_1()
    {
        await prefixed.HashDeleteAsync("key", "hashField", CommandFlags.None);
        mock.Verify(x => x.HashDeleteAsync("prefix:key", "hashField", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_delete_async_2()
    {
        RedisValue[] hashFields = Array.Empty<RedisValue>();
        await prefixed.HashDeleteAsync("key", hashFields, CommandFlags.None);
        mock.Verify(x => x.HashDeleteAsync("prefix:key", hashFields, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_exists_async()
    {
        await prefixed.HashExistsAsync("key", "hashField", CommandFlags.None);
        mock.Verify(x => x.HashExistsAsync("prefix:key", "hashField", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_get_all_async()
    {
        await prefixed.HashGetAllAsync("key", CommandFlags.None);
        mock.Verify(x => x.HashGetAllAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_get_async_1()
    {
        await prefixed.HashGetAsync("key", "hashField", CommandFlags.None);
        mock.Verify(x => x.HashGetAsync("prefix:key", "hashField", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_get_async_2()
    {
        RedisValue[] hashFields = Array.Empty<RedisValue>();
        await prefixed.HashGetAsync("key", hashFields, CommandFlags.None);
        mock.Verify(x => x.HashGetAsync("prefix:key", hashFields, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_increment_async_1()
    {
        await prefixed.HashIncrementAsync("key", "hashField", 123, CommandFlags.None);
        mock.Verify(x => x.HashIncrementAsync("prefix:key", "hashField", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_increment_async_2()
    {
        await prefixed.HashIncrementAsync("key", "hashField", 1.23, CommandFlags.None);
        mock.Verify(x => x.HashIncrementAsync("prefix:key", "hashField", 1.23, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_increment_async_3()
    {
        await prefixed.StringIncrementAsync("key", 123L, TimeSpan.FromSeconds(5), lowerBound: 10, upperBound: 200, flags: CommandFlags.None, options: IncrementOptions.None);
        mock.Verify(x => x.StringIncrementAsync("prefix:key", 123L, TimeSpan.FromSeconds(5), 10, 200, IncrementOptions.None, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_increment_async_4()
    {
        await prefixed.StringIncrementAsync("key", 1.23, TimeSpan.FromSeconds(5), lowerBound: -1.0, upperBound: 2.0, flags: CommandFlags.None, options: IncrementOptions.Saturate);
        mock.Verify(x => x.StringIncrementAsync("prefix:key", 1.23, TimeSpan.FromSeconds(5), -1.0, 2.0, IncrementOptions.Saturate, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_keys_async()
    {
        await prefixed.HashKeysAsync("key", CommandFlags.None);
        mock.Verify(x => x.HashKeysAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_length_async()
    {
        await prefixed.HashLengthAsync("key", CommandFlags.None);
        mock.Verify(x => x.HashLengthAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_set_async_1()
    {
        HashEntry[] hashFields = Array.Empty<HashEntry>();
        await prefixed.HashSetAsync("key", hashFields, CommandFlags.None);
        mock.Verify(x => x.HashSetAsync("prefix:key", hashFields, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_set_async_2()
    {
        await prefixed.HashSetAsync("key", "hashField", "value", When.Exists, CommandFlags.None);
        mock.Verify(x => x.HashSetAsync("prefix:key", "hashField", "value", When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_string_length_async()
    {
        await prefixed.HashStringLengthAsync("key", "field", CommandFlags.None);
        mock.Verify(x => x.HashStringLengthAsync("prefix:key", "field", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_values_async()
    {
        await prefixed.HashValuesAsync("key", CommandFlags.None);
        mock.Verify(x => x.HashValuesAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hyper_log_log_add_async_1()
    {
        await prefixed.HyperLogLogAddAsync("key", "value", CommandFlags.None);
        mock.Verify(x => x.HyperLogLogAddAsync("prefix:key", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hyper_log_log_add_async_2()
    {
        var values = Array.Empty<RedisValue>();
        await prefixed.HyperLogLogAddAsync("key", values, CommandFlags.None);
        mock.Verify(x => x.HyperLogLogAddAsync("prefix:key", values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hyper_log_log_length_async()
    {
        await prefixed.HyperLogLogLengthAsync("key", CommandFlags.None);
        mock.Verify(x => x.HyperLogLogLengthAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hyper_log_log_merge_async_1()
    {
        await prefixed.HyperLogLogMergeAsync("destination", "first", "second", CommandFlags.None);
        mock.Verify(x => x.HyperLogLogMergeAsync("prefix:destination", "prefix:first", "prefix:second", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hyper_log_log_merge_async_2()
    {
        RedisKey[] keys = ["a", "b"];
        await prefixed.HyperLogLogMergeAsync("destination", keys, CommandFlags.None);
        mock.Verify(x => x.HyperLogLogMergeAsync("prefix:destination", IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task identify_endpoint_async()
    {
        await prefixed.IdentifyEndpointAsync("key", CommandFlags.None);
        mock.Verify(x => x.IdentifyEndpointAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void is_connected()
    {
        prefixed.IsConnected("key", CommandFlags.None);
        mock.Verify(x => x.IsConnected("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_copy_async()
    {
        await prefixed.KeyCopyAsync("key", "destination", flags: CommandFlags.None);
        mock.Verify(x => x.KeyCopyAsync("prefix:key", "prefix:destination", -1, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_delete_async_1()
    {
        await prefixed.KeyDeleteAsync("key", CommandFlags.None);
        mock.Verify(x => x.KeyDeleteAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_delete_async_2()
    {
        RedisKey[] keys = ["a", "b"];
        await prefixed.KeyDeleteAsync(keys, CommandFlags.None);
        mock.Verify(x => x.KeyDeleteAsync(IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_dump_async()
    {
        await prefixed.KeyDumpAsync("key", CommandFlags.None);
        mock.Verify(x => x.KeyDumpAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_encoding_async()
    {
        await prefixed.KeyEncodingAsync("key", CommandFlags.None);
        mock.Verify(x => x.KeyEncodingAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_exists_async()
    {
        await prefixed.KeyExistsAsync("key", CommandFlags.None);
        mock.Verify(x => x.KeyExistsAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_expire_async_1()
    {
        TimeSpan expiry = TimeSpan.FromSeconds(123);
        await prefixed.KeyExpireAsync("key", expiry, CommandFlags.None);
        mock.Verify(x => x.KeyExpireAsync("prefix:key", expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_expire_async_2()
    {
        DateTime expiry = DateTime.Now;
        await prefixed.KeyExpireAsync("key", expiry, CommandFlags.None);
        mock.Verify(x => x.KeyExpireAsync("prefix:key", expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_expire_async_3()
    {
        TimeSpan expiry = TimeSpan.FromSeconds(123);
        await prefixed.KeyExpireAsync("key", expiry, ExpireWhen.HasNoExpiry, CommandFlags.None);
        mock.Verify(x => x.KeyExpireAsync("prefix:key", expiry, ExpireWhen.HasNoExpiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_expire_async_4()
    {
        DateTime expiry = DateTime.Now;
        await prefixed.KeyExpireAsync("key", expiry, ExpireWhen.HasNoExpiry, CommandFlags.None);
        mock.Verify(x => x.KeyExpireAsync("prefix:key", expiry, ExpireWhen.HasNoExpiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_expire_time_async()
    {
        await prefixed.KeyExpireTimeAsync("key", CommandFlags.None);
        mock.Verify(x => x.KeyExpireTimeAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_frequency_async()
    {
        await prefixed.KeyFrequencyAsync("key", CommandFlags.None);
        mock.Verify(x => x.KeyFrequencyAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_migrate_async()
    {
        EndPoint toServer = new IPEndPoint(IPAddress.Loopback, 123);
        await prefixed.KeyMigrateAsync("key", toServer, 123, 456, MigrateOptions.Copy, CommandFlags.None);
        mock.Verify(x => x.KeyMigrateAsync("prefix:key", toServer, 123, 456, MigrateOptions.Copy, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_move_async()
    {
        await prefixed.KeyMoveAsync("key", 123, CommandFlags.None);
        mock.Verify(x => x.KeyMoveAsync("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_persist_async()
    {
        await prefixed.KeyPersistAsync("key", CommandFlags.None);
        mock.Verify(x => x.KeyPersistAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public Task key_random_async()
    {
        return Assert.ThrowsAsync<NotSupportedException>(() => prefixed.KeyRandomAsync());
    }

    [Fact]
    public async Task key_ref_count_async()
    {
        await prefixed.KeyRefCountAsync("key", CommandFlags.None);
        mock.Verify(x => x.KeyRefCountAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_rename_async()
    {
        await prefixed.KeyRenameAsync("key", "newKey", When.Exists, CommandFlags.None);
        mock.Verify(x => x.KeyRenameAsync("prefix:key", "prefix:newKey", When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_restore_async()
    {
        byte[] value = Array.Empty<byte>();
        TimeSpan expiry = TimeSpan.FromSeconds(123);
        await prefixed.KeyRestoreAsync("key", value, expiry, CommandFlags.None);
        mock.Verify(x => x.KeyRestoreAsync("prefix:key", value, expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_time_to_live_async()
    {
        await prefixed.KeyTimeToLiveAsync("key", CommandFlags.None);
        mock.Verify(x => x.KeyTimeToLiveAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_type_async()
    {
        await prefixed.KeyTypeAsync("key", CommandFlags.None);
        mock.Verify(x => x.KeyTypeAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_get_by_index_async()
    {
        await prefixed.ListGetByIndexAsync("key", 123, CommandFlags.None);
        mock.Verify(x => x.ListGetByIndexAsync("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_insert_after_async()
    {
        await prefixed.ListInsertAfterAsync("key", "pivot", "value", CommandFlags.None);
        mock.Verify(x => x.ListInsertAfterAsync("prefix:key", "pivot", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_insert_before_async()
    {
        await prefixed.ListInsertBeforeAsync("key", "pivot", "value", CommandFlags.None);
        mock.Verify(x => x.ListInsertBeforeAsync("prefix:key", "pivot", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_left_pop_async()
    {
        await prefixed.ListLeftPopAsync("key", CommandFlags.None);
        mock.Verify(x => x.ListLeftPopAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_left_pop_async_1()
    {
        await prefixed.ListLeftPopAsync("key", 123, CommandFlags.None);
        mock.Verify(x => x.ListLeftPopAsync("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_left_push_async_1()
    {
        await prefixed.ListLeftPushAsync("key", "value", When.Exists, CommandFlags.None);
        mock.Verify(x => x.ListLeftPushAsync("prefix:key", "value", When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_left_push_async_2()
    {
        RedisValue[] values = Array.Empty<RedisValue>();
        await prefixed.ListLeftPushAsync("key", values, CommandFlags.None);
        mock.Verify(x => x.ListLeftPushAsync("prefix:key", values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_left_push_async_3()
    {
        RedisValue[] values = ["value1", "value2"];
        await prefixed.ListLeftPushAsync("key", values, When.Exists, CommandFlags.None);
        mock.Verify(x => x.ListLeftPushAsync("prefix:key", values, When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_length_async()
    {
        await prefixed.ListLengthAsync("key", CommandFlags.None);
        mock.Verify(x => x.ListLengthAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_move_async()
    {
        await prefixed.ListMoveAsync("key", "destination", ListSide.Left, ListSide.Right, CommandFlags.None);
        mock.Verify(x => x.ListMoveAsync("prefix:key", "prefix:destination", ListSide.Left, ListSide.Right, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_range_async()
    {
        await prefixed.ListRangeAsync("key", 123, 456, CommandFlags.None);
        mock.Verify(x => x.ListRangeAsync("prefix:key", 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_remove_async()
    {
        await prefixed.ListRemoveAsync("key", "value", 123, CommandFlags.None);
        mock.Verify(x => x.ListRemoveAsync("prefix:key", "value", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_right_pop_async()
    {
        await prefixed.ListRightPopAsync("key", CommandFlags.None);
        mock.Verify(x => x.ListRightPopAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_right_pop_async_1()
    {
        await prefixed.ListRightPopAsync("key", 123, CommandFlags.None);
        mock.Verify(x => x.ListRightPopAsync("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_right_pop_left_push_async()
    {
        await prefixed.ListRightPopLeftPushAsync("source", "destination", CommandFlags.None);
        mock.Verify(x => x.ListRightPopLeftPushAsync("prefix:source", "prefix:destination", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_right_push_async_1()
    {
        await prefixed.ListRightPushAsync("key", "value", When.Exists, CommandFlags.None);
        mock.Verify(x => x.ListRightPushAsync("prefix:key", "value", When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_right_push_async_2()
    {
        RedisValue[] values = Array.Empty<RedisValue>();
        await prefixed.ListRightPushAsync("key", values, CommandFlags.None);
        mock.Verify(x => x.ListRightPushAsync("prefix:key", values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_right_push_async_3()
    {
        RedisValue[] values = ["value1", "value2"];
        await prefixed.ListRightPushAsync("key", values, When.Exists, CommandFlags.None);
        mock.Verify(x => x.ListRightPushAsync("prefix:key", values, When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_set_by_index_async()
    {
        await prefixed.ListSetByIndexAsync("key", 123, "value", CommandFlags.None);
        mock.Verify(x => x.ListSetByIndexAsync("prefix:key", 123, "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task list_trim_async()
    {
        await prefixed.ListTrimAsync("key", 123, 456, CommandFlags.None);
        mock.Verify(x => x.ListTrimAsync("prefix:key", 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task lock_extend_async()
    {
        TimeSpan expiry = TimeSpan.FromSeconds(123);
        await prefixed.LockExtendAsync("key", "value", expiry, CommandFlags.None);
        mock.Verify(x => x.LockExtendAsync("prefix:key", "value", expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task lock_query_async()
    {
        await prefixed.LockQueryAsync("key", CommandFlags.None);
        mock.Verify(x => x.LockQueryAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task lock_release_async()
    {
        await prefixed.LockReleaseAsync("key", "value", CommandFlags.None);
        mock.Verify(x => x.LockReleaseAsync("prefix:key", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task lock_take_async()
    {
        TimeSpan expiry = TimeSpan.FromSeconds(123);
        await prefixed.LockTakeAsync("key", "value", expiry, CommandFlags.None);
        mock.Verify(x => x.LockTakeAsync("prefix:key", "value", expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task publish_async()
    {
        await prefixed.PublishAsync(RedisChannel.Literal("channel"), "message", CommandFlags.None);
        mock.Verify(x => x.PublishAsync(RedisChannel.Literal("prefix:channel"), "message", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task script_evaluate_async_1()
    {
        byte[] hash = Array.Empty<byte>();
        RedisValue[] values = Array.Empty<RedisValue>();
        RedisKey[] keys = ["a", "b"];
        await prefixed.ScriptEvaluateAsync(hash, keys, values, CommandFlags.None);
        mock.Verify(x => x.ScriptEvaluateAsync(hash, IsKeys("prefix:a", "prefix:b"), values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task script_evaluate_async_2()
    {
        RedisValue[] values = Array.Empty<RedisValue>();
        RedisKey[] keys = ["a", "b"];
        await prefixed.ScriptEvaluateAsync("script", keys, values, CommandFlags.None);
        mock.Verify(x => x.ScriptEvaluateAsync(script: "script", keys: IsKeys("prefix:a", "prefix:b"), values: values, flags: CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_add_async_1()
    {
        await prefixed.SetAddAsync("key", "value", CommandFlags.None);
        mock.Verify(x => x.SetAddAsync("prefix:key", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_add_async_2()
    {
        RedisValue[] values = Array.Empty<RedisValue>();
        await prefixed.SetAddAsync("key", values, CommandFlags.None);
        mock.Verify(x => x.SetAddAsync("prefix:key", values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_combine_and_store_async_1()
    {
        await prefixed.SetCombineAndStoreAsync(SetOperation.Intersect, "destination", "first", "second", CommandFlags.None);
        mock.Verify(x => x.SetCombineAndStoreAsync(SetOperation.Intersect, "prefix:destination", "prefix:first", "prefix:second", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_combine_and_store_async_2()
    {
        RedisKey[] keys = ["a", "b"];
        await prefixed.SetCombineAndStoreAsync(SetOperation.Intersect, "destination", keys, CommandFlags.None);
        mock.Verify(x => x.SetCombineAndStoreAsync(SetOperation.Intersect, "prefix:destination", IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_combine_async_1()
    {
        await prefixed.SetCombineAsync(SetOperation.Intersect, "first", "second", CommandFlags.None);
        mock.Verify(x => x.SetCombineAsync(SetOperation.Intersect, "prefix:first", "prefix:second", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_combine_async_2()
    {
        RedisKey[] keys = ["a", "b"];
        await prefixed.SetCombineAsync(SetOperation.Intersect, keys, CommandFlags.None);
        mock.Verify(x => x.SetCombineAsync(SetOperation.Intersect, IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_contains_async()
    {
        await prefixed.SetContainsAsync("key", "value", CommandFlags.None);
        mock.Verify(x => x.SetContainsAsync("prefix:key", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_contains_async_2()
    {
        RedisValue[] values = ["value1", "value2"];
        await prefixed.SetContainsAsync("key", values, CommandFlags.None);
        mock.Verify(x => x.SetContainsAsync("prefix:key", values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_intersection_length_async()
    {
        await prefixed.SetIntersectionLengthAsync(["key1", "key2"]);
        mock.Verify(x => x.SetIntersectionLengthAsync(IsKeys("prefix:key1", "prefix:key2"), 0, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_combine_length_async()
    {
        await prefixed.SetCombineLengthAsync(SetOperation.Union, ["key1", "key2"]);
        mock.Verify(x => x.SetCombineLengthAsync(SetOperation.Union, IsKeys("prefix:key1", "prefix:key2"), 0, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_length_async()
    {
        await prefixed.SetLengthAsync("key", CommandFlags.None);
        mock.Verify(x => x.SetLengthAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_members_async()
    {
        await prefixed.SetMembersAsync("key", CommandFlags.None);
        mock.Verify(x => x.SetMembersAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_move_async()
    {
        await prefixed.SetMoveAsync("source", "destination", "value", CommandFlags.None);
        mock.Verify(x => x.SetMoveAsync("prefix:source", "prefix:destination", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_pop_async_1()
    {
        await prefixed.SetPopAsync("key", CommandFlags.None);
        mock.Verify(x => x.SetPopAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());

        await prefixed.SetPopAsync("key", 5, CommandFlags.None);
        mock.Verify(x => x.SetPopAsync("prefix:key", 5, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_pop_async_2()
    {
        await prefixed.SetPopAsync("key", 5, CommandFlags.None);
        mock.Verify(x => x.SetPopAsync("prefix:key", 5, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_random_member_async()
    {
        await prefixed.SetRandomMemberAsync("key", CommandFlags.None);
        mock.Verify(x => x.SetRandomMemberAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_random_members_async()
    {
        await prefixed.SetRandomMembersAsync("key", 123, CommandFlags.None);
        mock.Verify(x => x.SetRandomMembersAsync("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_remove_async_1()
    {
        await prefixed.SetRemoveAsync("key", "value", CommandFlags.None);
        mock.Verify(x => x.SetRemoveAsync("prefix:key", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task set_remove_async_2()
    {
        RedisValue[] values = Array.Empty<RedisValue>();
        await prefixed.SetRemoveAsync("key", values, CommandFlags.None);
        mock.Verify(x => x.SetRemoveAsync("prefix:key", values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sort_and_store_async()
    {
        RedisValue[] get = ["a", "#"];

        await prefixed.SortAndStoreAsync("destination", "key", 123, 456, Order.Descending, SortType.Alphabetic, "nosort", get, CommandFlags.None);
        await prefixed.SortAndStoreAsync("destination", "key", 123, 456, Order.Descending, SortType.Alphabetic, "by", get, CommandFlags.None);

        mock.Verify(x => x.SortAndStoreAsync("prefix:destination", "prefix:key", 123, 456, Order.Descending, SortType.Alphabetic, "nosort", IsValues("prefix:a", "#"), CommandFlags.None), Times.AtLeastOnce());
        mock.Verify(x => x.SortAndStoreAsync("prefix:destination", "prefix:key", 123, 456, Order.Descending, SortType.Alphabetic, "prefix:by", IsValues("prefix:a", "#"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sort_async()
    {
        RedisValue[] get = ["a", "#"];

        await prefixed.SortAsync("key", 123, 456, Order.Descending, SortType.Alphabetic, "nosort", get, CommandFlags.None);
        await prefixed.SortAsync("key", 123, 456, Order.Descending, SortType.Alphabetic, "by", get, CommandFlags.None);

        mock.Verify(x => x.SortAsync("prefix:key", 123, 456, Order.Descending, SortType.Alphabetic, "nosort", IsValues("prefix:a", "#"), CommandFlags.None), Times.AtLeastOnce());
        mock.Verify(x => x.SortAsync("prefix:key", 123, 456, Order.Descending, SortType.Alphabetic, "prefix:by", IsValues("prefix:a", "#"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_add_async_1()
    {
        await prefixed.SortedSetAddAsync("key", "member", 1.23, When.Exists, CommandFlags.None);
        mock.Verify(x => x.SortedSetAddAsync("prefix:key", "member", 1.23, When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_add_async_2()
    {
        SortedSetEntry[] values = Array.Empty<SortedSetEntry>();
        await prefixed.SortedSetAddAsync("key", values, When.Exists, CommandFlags.None);
        mock.Verify(x => x.SortedSetAddAsync("prefix:key", values, When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_add_async_3()
    {
        SortedSetEntry[] values = Array.Empty<SortedSetEntry>();
        await prefixed.SortedSetAddAsync("key", values, SortedSetWhen.GreaterThan, CommandFlags.None);
        mock.Verify(x => x.SortedSetAddAsync("prefix:key", values, SortedSetWhen.GreaterThan, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_combine_async()
    {
        await prefixed.SortedSetCombineAsync(SetOperation.Intersect, ["a", "b"]);
        mock.Verify(x => x.SortedSetCombineAsync(SetOperation.Intersect, IsKeys("prefix:a", "prefix:b"), null, Aggregate.Sum, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_combine_with_scores_async()
    {
        await prefixed.SortedSetCombineWithScoresAsync(SetOperation.Intersect, ["a", "b"]);
        mock.Verify(x => x.SortedSetCombineWithScoresAsync(SetOperation.Intersect, IsKeys("prefix:a", "prefix:b"), null, Aggregate.Sum, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_combine_and_store_async_1()
    {
        await prefixed.SortedSetCombineAndStoreAsync(SetOperation.Intersect, "destination", "first", "second", Aggregate.Max, CommandFlags.None);
        mock.Verify(x => x.SortedSetCombineAndStoreAsync(SetOperation.Intersect, "prefix:destination", "prefix:first", "prefix:second", Aggregate.Max, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_combine_and_store_async_2()
    {
        RedisKey[] keys = ["a", "b"];
        await prefixed.SetCombineAndStoreAsync(SetOperation.Intersect, "destination", keys, CommandFlags.None);
        mock.Verify(x => x.SetCombineAndStoreAsync(SetOperation.Intersect, "prefix:destination", IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_decrement_async()
    {
        await prefixed.SortedSetDecrementAsync("key", "member", 1.23, CommandFlags.None);
        mock.Verify(x => x.SortedSetDecrementAsync("prefix:key", "member", 1.23, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_increment_async()
    {
        await prefixed.SortedSetIncrementAsync("key", "member", 1.23, CommandFlags.None);
        mock.Verify(x => x.SortedSetIncrementAsync("prefix:key", "member", 1.23, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_increment_async_when()
    {
        await prefixed.SortedSetIncrementAsync("key", "member", 1.23, ValueCondition.Exists, CommandFlags.None);
        mock.Verify(x => x.SortedSetIncrementAsync("prefix:key", "member", 1.23, ValueCondition.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_intersection_length_async()
    {
        await prefixed.SortedSetIntersectionLengthAsync(["a", "b"], 1, CommandFlags.None);
        mock.Verify(x => x.SortedSetIntersectionLengthAsync(IsKeys("prefix:a", "prefix:b"), 1, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_length_async()
    {
        await prefixed.SortedSetLengthAsync("key", 1.23, 1.23, Exclude.Start, CommandFlags.None);
        mock.Verify(x => x.SortedSetLengthAsync("prefix:key", 1.23, 1.23, Exclude.Start, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_length_by_value_async()
    {
        await prefixed.SortedSetLengthByValueAsync("key", "min", "max", Exclude.Start, CommandFlags.None);
        mock.Verify(x => x.SortedSetLengthByValueAsync("prefix:key", "min", "max", Exclude.Start, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_random_member_async()
    {
        await prefixed.SortedSetRandomMemberAsync("key", CommandFlags.None);
        mock.Verify(x => x.SortedSetRandomMemberAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_random_members_async()
    {
        await prefixed.SortedSetRandomMembersAsync("key", 2, CommandFlags.None);
        mock.Verify(x => x.SortedSetRandomMembersAsync("prefix:key", 2, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_random_member_with_scores_async()
    {
        await prefixed.SortedSetRandomMembersWithScoresAsync("key", 2, CommandFlags.None);
        mock.Verify(x => x.SortedSetRandomMembersWithScoresAsync("prefix:key", 2, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_range_by_rank_async()
    {
        await prefixed.SortedSetRangeByRankAsync("key", 123, 456, Order.Descending, CommandFlags.None);
        mock.Verify(x => x.SortedSetRangeByRankAsync("prefix:key", 123, 456, Order.Descending, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_range_by_rank_with_scores_async()
    {
        await prefixed.SortedSetRangeByRankWithScoresAsync("key", 123, 456, Order.Descending, CommandFlags.None);
        mock.Verify(x => x.SortedSetRangeByRankWithScoresAsync("prefix:key", 123, 456, Order.Descending, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_range_by_score_async()
    {
        await prefixed.SortedSetRangeByScoreAsync("key", 1.23, 1.23, Exclude.Start, Order.Descending, 123, 456, CommandFlags.None);
        mock.Verify(x => x.SortedSetRangeByScoreAsync("prefix:key", 1.23, 1.23, Exclude.Start, Order.Descending, 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_range_by_score_with_scores_async()
    {
        await prefixed.SortedSetRangeByScoreWithScoresAsync("key", 1.23, 1.23, Exclude.Start, Order.Descending, 123, 456, CommandFlags.None);
        mock.Verify(x => x.SortedSetRangeByScoreWithScoresAsync("prefix:key", 1.23, 1.23, Exclude.Start, Order.Descending, 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_range_by_value_async()
    {
        await prefixed.SortedSetRangeByValueAsync("key", "min", "max", Exclude.Start, 123, 456, CommandFlags.None);
        mock.Verify(x => x.SortedSetRangeByValueAsync("prefix:key", "min", "max", Exclude.Start, Order.Ascending, 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_range_by_value_desc_async()
    {
        await prefixed.SortedSetRangeByValueAsync("key", "min", "max", Exclude.Start, Order.Descending, 123, 456, CommandFlags.None);
        mock.Verify(x => x.SortedSetRangeByValueAsync("prefix:key", "min", "max", Exclude.Start, Order.Descending, 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_rank_async()
    {
        await prefixed.SortedSetRankAsync("key", "member", Order.Descending, CommandFlags.None);
        mock.Verify(x => x.SortedSetRankAsync("prefix:key", "member", Order.Descending, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_remove_async_1()
    {
        await prefixed.SortedSetRemoveAsync("key", "member", CommandFlags.None);
        mock.Verify(x => x.SortedSetRemoveAsync("prefix:key", "member", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_remove_async_2()
    {
        RedisValue[] members = Array.Empty<RedisValue>();
        await prefixed.SortedSetRemoveAsync("key", members, CommandFlags.None);
        mock.Verify(x => x.SortedSetRemoveAsync("prefix:key", members, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_remove_range_by_rank_async()
    {
        await prefixed.SortedSetRemoveRangeByRankAsync("key", 123, 456, CommandFlags.None);
        mock.Verify(x => x.SortedSetRemoveRangeByRankAsync("prefix:key", 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_remove_range_by_score_async()
    {
        await prefixed.SortedSetRemoveRangeByScoreAsync("key", 1.23, 1.23, Exclude.Start, CommandFlags.None);
        mock.Verify(x => x.SortedSetRemoveRangeByScoreAsync("prefix:key", 1.23, 1.23, Exclude.Start, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_remove_range_by_value_async()
    {
        await prefixed.SortedSetRemoveRangeByValueAsync("key", "min", "max", Exclude.Start, CommandFlags.None);
        mock.Verify(x => x.SortedSetRemoveRangeByValueAsync("prefix:key", "min", "max", Exclude.Start, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_score_async()
    {
        await prefixed.SortedSetScoreAsync("key", "member", CommandFlags.None);
        mock.Verify(x => x.SortedSetScoreAsync("prefix:key", "member", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_score_async_multiple()
    {
        var values = new RedisValue[] { "member1", "member2" };
        await prefixed.SortedSetScoresAsync("key", values, CommandFlags.None);
        mock.Verify(x => x.SortedSetScoresAsync("prefix:key", values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task sorted_set_update_async()
    {
        SortedSetEntry[] values = Array.Empty<SortedSetEntry>();
        await prefixed.SortedSetUpdateAsync("key", values, SortedSetWhen.GreaterThan, CommandFlags.None);
        mock.Verify(x => x.SortedSetUpdateAsync("prefix:key", values, SortedSetWhen.GreaterThan, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_acknowledge_async_1()
    {
        await prefixed.StreamAcknowledgeAsync("key", "group", "0-0", CommandFlags.None);
        mock.Verify(x => x.StreamAcknowledgeAsync("prefix:key", "group", "0-0", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_acknowledge_async_2()
    {
        var messageIds = new RedisValue[] { "0-0", "0-1", "0-2" };
        await prefixed.StreamAcknowledgeAsync("key", "group", messageIds, CommandFlags.None);
        mock.Verify(x => x.StreamAcknowledgeAsync("prefix:key", "group", messageIds, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_negative_acknowledge_async_1()
    {
        await prefixed.StreamNegativeAcknowledgeAsync("key", "group", StreamNackMode.Fail, "0-0", CommandFlags.None);
        mock.Verify(x => x.StreamNegativeAcknowledgeAsync("prefix:key", "group", StreamNackMode.Fail, "0-0", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_negative_acknowledge_async_2()
    {
        var messageIds = new RedisValue[] { "0-0", "0-1", "0-2" };
        await prefixed.StreamNegativeAcknowledgeAsync("key", "group", StreamNackMode.Fail, messageIds, CommandFlags.None);
        mock.Verify(x => x.StreamNegativeAcknowledgeAsync("prefix:key", "group", StreamNackMode.Fail, messageIds, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_add_async_1()
    {
        await prefixed.StreamAddAsync("key", "field1", "value1", "*", 1000, true, CommandFlags.None);
        mock.Verify(x => x.StreamAddAsync("prefix:key", "field1", "value1", "*", 1000, true, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_add_async_2()
    {
        var fields = Array.Empty<NameValueEntry>();
        await prefixed.StreamAddAsync("key", fields, "*", 1000, true, CommandFlags.None);
        mock.Verify(x => x.StreamAddAsync("prefix:key", fields, "*", 1000, true, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_auto_claim_async()
    {
        await prefixed.StreamAutoClaimAsync("key", "group", "consumer", 0, "0-0", 100, CommandFlags.None);
        mock.Verify(x => x.StreamAutoClaimAsync("prefix:key", "group", "consumer", 0, "0-0", 100, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_auto_claim_ids_only_async()
    {
        await prefixed.StreamAutoClaimIdsOnlyAsync("key", "group", "consumer", 0, "0-0", 100, CommandFlags.None);
        mock.Verify(x => x.StreamAutoClaimIdsOnlyAsync("prefix:key", "group", "consumer", 0, "0-0", 100, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_claim_messages_async()
    {
        var messageIds = Array.Empty<RedisValue>();
        await prefixed.StreamClaimAsync("key", "group", "consumer", 1000, messageIds, CommandFlags.None);
        mock.Verify(x => x.StreamClaimAsync("prefix:key", "group", "consumer", 1000, messageIds, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_claim_messages_returning_ids_async()
    {
        var messageIds = Array.Empty<RedisValue>();
        await prefixed.StreamClaimIdsOnlyAsync("key", "group", "consumer", 1000, messageIds, CommandFlags.None);
        mock.Verify(x => x.StreamClaimIdsOnlyAsync("prefix:key", "group", "consumer", 1000, messageIds, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_consumer_info_get_async()
    {
        await prefixed.StreamConsumerInfoAsync("key", "group", CommandFlags.None);
        mock.Verify(x => x.StreamConsumerInfoAsync("prefix:key", "group", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_consumer_group_set_position_async()
    {
        await prefixed.StreamConsumerGroupSetPositionAsync("key", "group", StreamPosition.Beginning, CommandFlags.None);
        mock.Verify(x => x.StreamConsumerGroupSetPositionAsync("prefix:key", "group", StreamPosition.Beginning, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_create_consumer_group_async()
    {
        await prefixed.StreamCreateConsumerGroupAsync("key", "group", "0-0", false, CommandFlags.None);
        mock.Verify(x => x.StreamCreateConsumerGroupAsync("prefix:key", "group", "0-0", false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_group_info_get_async()
    {
        await prefixed.StreamGroupInfoAsync("key", CommandFlags.None);
        mock.Verify(x => x.StreamGroupInfoAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_info_get_async()
    {
        await prefixed.StreamInfoAsync("key", CommandFlags.None);
        mock.Verify(x => x.StreamInfoAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_length_async()
    {
        await prefixed.StreamLengthAsync("key", CommandFlags.None);
        mock.Verify(x => x.StreamLengthAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_messages_delete_async()
    {
        var messageIds = Array.Empty<RedisValue>();
        await prefixed.StreamDeleteAsync("key", messageIds, CommandFlags.None);
        mock.Verify(x => x.StreamDeleteAsync("prefix:key", messageIds, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_delete_consumer_async()
    {
        await prefixed.StreamDeleteConsumerAsync("key", "group", "consumer", CommandFlags.None);
        mock.Verify(x => x.StreamDeleteConsumerAsync("prefix:key", "group", "consumer", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_delete_consumer_group_async()
    {
        await prefixed.StreamDeleteConsumerGroupAsync("key", "group", CommandFlags.None);
        mock.Verify(x => x.StreamDeleteConsumerGroupAsync("prefix:key", "group", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_pending_info_get_async()
    {
        await prefixed.StreamPendingAsync("key", "group", CommandFlags.None);
        mock.Verify(x => x.StreamPendingAsync("prefix:key", "group", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_pending_message_info_get_async()
    {
        await prefixed.StreamPendingMessagesAsync("key", "group", 10, RedisValue.Null, "-", "+", 1000, CommandFlags.None);
        mock.Verify(x => x.StreamPendingMessagesAsync("prefix:key", "group", 10, RedisValue.Null, "-", "+", 1000, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_range_async()
    {
        await prefixed.StreamRangeAsync("key", "-", "+", null, Order.Ascending, CommandFlags.None);
        mock.Verify(x => x.StreamRangeAsync("prefix:key", "-", "+", null, Order.Ascending, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_read_async_1()
    {
        var streamPositions = Array.Empty<StreamPosition>();
        await prefixed.StreamReadAsync(streamPositions, null, CommandFlags.None);
        mock.Verify(x => x.StreamReadAsync(streamPositions, null, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_read_async_2()
    {
        await prefixed.StreamReadAsync("key", "0-0", null, CommandFlags.None);
        mock.Verify(x => x.StreamReadAsync("prefix:key", "0-0", null, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_read_group_async_1()
    {
        await prefixed.StreamReadGroupAsync("key", "group", "consumer", StreamPosition.Beginning, 10, false, CommandFlags.None);
        mock.Verify(x => x.StreamReadGroupAsync("prefix:key", "group", "consumer", StreamPosition.Beginning, 10, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_stream_read_group_async_2()
    {
        var streamPositions = Array.Empty<StreamPosition>();
        await prefixed.StreamReadGroupAsync(streamPositions, "group", "consumer", 10, false, CommandFlags.None);
        mock.Verify(x => x.StreamReadGroupAsync(streamPositions, "group", "consumer", 10, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_trim_async()
    {
        await prefixed.StreamTrimAsync("key", 1000, true, CommandFlags.None);
        mock.Verify(x => x.StreamTrimAsync("prefix:key", 1000, true, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_trim_by_min_id_async()
    {
        await prefixed.StreamTrimByMinIdAsync("key", 1111111111);
        mock.Verify(x => x.StreamTrimByMinIdAsync("prefix:key", 1111111111), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_trim_by_min_id_async_with_approximate()
    {
        await prefixed.StreamTrimByMinIdAsync("key", 1111111111, useApproximateMaxLength: true);
        mock.Verify(x => x.StreamTrimByMinIdAsync("prefix:key", 1111111111, useApproximateMaxLength: true), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_trim_by_min_id_async_with_approximate_and_limit()
    {
        await prefixed.StreamTrimByMinIdAsync("key", 1111111111, useApproximateMaxLength: true, limit: 100);
        mock.Verify(x => x.StreamTrimByMinIdAsync("prefix:key", 1111111111, useApproximateMaxLength: true, limit: 100), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_append_async()
    {
        await prefixed.StringAppendAsync("key", "value", CommandFlags.None);
        mock.Verify(x => x.StringAppendAsync("prefix:key", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_bit_count_async()
    {
        await prefixed.StringBitCountAsync("key", 123, 456, CommandFlags.None);
        mock.Verify(x => x.StringBitCountAsync("prefix:key", 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_bit_count_async_2()
    {
        await prefixed.StringBitCountAsync("key", 123, 456, StringIndexType.Byte, CommandFlags.None);
        mock.Verify(x => x.StringBitCountAsync("prefix:key", 123, 456, StringIndexType.Byte, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_bit_operation_async_1()
    {
        await prefixed.StringBitOperationAsync(Bitwise.Xor, "destination", "first", "second", CommandFlags.None);
        mock.Verify(x => x.StringBitOperationAsync(Bitwise.Xor, "prefix:destination", "prefix:first", "prefix:second", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_bit_operation_async_2()
    {
        RedisKey[] keys = ["a", "b"];
        await prefixed.StringBitOperationAsync(Bitwise.Xor, "destination", keys, CommandFlags.None);
        mock.Verify(x => x.StringBitOperationAsync(Bitwise.Xor, "prefix:destination", IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_bit_operation_async_diff()
    {
        RedisKey[] keys = ["x", "y1", "y2"];
        await prefixed.StringBitOperationAsync(Bitwise.Diff, "destination", keys, CommandFlags.None);
        mock.Verify(x => x.StringBitOperationAsync(Bitwise.Diff, "prefix:destination", IsKeys("prefix:x", "prefix:y1", "prefix:y2"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_bit_operation_async_diff1()
    {
        RedisKey[] keys = ["x", "y1", "y2"];
        await prefixed.StringBitOperationAsync(Bitwise.Diff1, "destination", keys, CommandFlags.None);
        mock.Verify(x => x.StringBitOperationAsync(Bitwise.Diff1, "prefix:destination", IsKeys("prefix:x", "prefix:y1", "prefix:y2"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_bit_operation_async_and_or()
    {
        RedisKey[] keys = ["x", "y1", "y2"];
        await prefixed.StringBitOperationAsync(Bitwise.AndOr, "destination", keys, CommandFlags.None);
        mock.Verify(x => x.StringBitOperationAsync(Bitwise.AndOr, "prefix:destination", IsKeys("prefix:x", "prefix:y1", "prefix:y2"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_bit_operation_async_one()
    {
        RedisKey[] keys = ["a", "b", "c"];
        await prefixed.StringBitOperationAsync(Bitwise.One, "destination", keys, CommandFlags.None);
        mock.Verify(x => x.StringBitOperationAsync(Bitwise.One, "prefix:destination", IsKeys("prefix:a", "prefix:b", "prefix:c"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_bit_position_async()
    {
        await prefixed.StringBitPositionAsync("key", true, 123, 456, CommandFlags.None);
        mock.Verify(x => x.StringBitPositionAsync("prefix:key", true, 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_bit_position_async_2()
    {
        await prefixed.StringBitPositionAsync("key", true, 123, 456, StringIndexType.Byte, CommandFlags.None);
        mock.Verify(x => x.StringBitPositionAsync("prefix:key", true, 123, 456, StringIndexType.Byte, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_decrement_async_1()
    {
        await prefixed.StringDecrementAsync("key", 123, CommandFlags.None);
        mock.Verify(x => x.StringDecrementAsync("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_decrement_async_2()
    {
        await prefixed.StringDecrementAsync("key", 1.23, CommandFlags.None);
        mock.Verify(x => x.StringDecrementAsync("prefix:key", 1.23, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_get_async_1()
    {
        await prefixed.StringGetAsync("key", CommandFlags.None);
        mock.Verify(x => x.StringGetAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_get_async_2()
    {
        RedisKey[] keys = ["a", "b"];
        await prefixed.StringGetAsync(keys, CommandFlags.None);
        mock.Verify(x => x.StringGetAsync(IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_get_bit_async()
    {
        await prefixed.StringGetBitAsync("key", 123, CommandFlags.None);
        mock.Verify(x => x.StringGetBitAsync("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_get_range_async()
    {
        await prefixed.StringGetRangeAsync("key", 123, 456, CommandFlags.None);
        mock.Verify(x => x.StringGetRangeAsync("prefix:key", 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_get_set_async()
    {
        await prefixed.StringGetSetAsync("key", "value", CommandFlags.None);
        mock.Verify(x => x.StringGetSetAsync("prefix:key", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_get_delete_async()
    {
        await prefixed.StringGetDeleteAsync("key", CommandFlags.None);
        mock.Verify(x => x.StringGetDeleteAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_get_with_expiry_async()
    {
        await prefixed.StringGetWithExpiryAsync("key", CommandFlags.None);
        mock.Verify(x => x.StringGetWithExpiryAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_increment_async_1()
    {
        await prefixed.StringIncrementAsync("key", 123, CommandFlags.None);
        mock.Verify(x => x.StringIncrementAsync("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_increment_async_2()
    {
        await prefixed.StringIncrementAsync("key", 1.23, CommandFlags.None);
        mock.Verify(x => x.StringIncrementAsync("prefix:key", 1.23, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_length_async()
    {
        await prefixed.StringLengthAsync("key", CommandFlags.None);
        mock.Verify(x => x.StringLengthAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_set_async_1()
    {
        TimeSpan expiry = TimeSpan.FromSeconds(123);
        await prefixed.StringSetAsync("key", "value", expiry, When.Exists, CommandFlags.None);
        mock.Verify(x => x.StringSetAsync("prefix:key", "value", expiry, When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_set_async_2()
    {
        TimeSpan? expiry = null;
        await prefixed.StringSetAsync("key", "value", expiry, true, When.Exists, CommandFlags.None);
        mock.Verify(x => x.StringSetAsync("prefix:key", "value", expiry, true, When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_set_async_3()
    {
        KeyValuePair<RedisKey, RedisValue>[] values = [new KeyValuePair<RedisKey, RedisValue>("a", "x"), new KeyValuePair<RedisKey, RedisValue>("b", "y")];
        Expression<Func<KeyValuePair<RedisKey, RedisValue>[], bool>> valid = _ => _.Length == 2 && _[0].Key == "prefix:a" && _[0].Value == "x" && _[1].Key == "prefix:b" && _[1].Value == "y";
        await prefixed.StringSetAsync(values, When.Exists, CommandFlags.None);
        mock.Verify(x => x.StringSetAsync(It.Is(valid), When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_set_async_compat()
    {
        TimeSpan expiry = TimeSpan.FromSeconds(123);
        await prefixed.StringSetAsync("key", "value", expiry, When.Exists);
        mock.Verify(x => x.StringSetAsync("prefix:key", "value", expiry, When.Exists), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_set_bit_async()
    {
        await prefixed.StringSetBitAsync("key", 123, true, CommandFlags.None);
        mock.Verify(x => x.StringSetBitAsync("prefix:key", 123, true, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_set_range_async()
    {
        await prefixed.StringSetRangeAsync("key", 123, "value", CommandFlags.None);
        mock.Verify(x => x.StringSetRangeAsync("prefix:key", 123, "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_touch_async_1()
    {
        await prefixed.KeyTouchAsync("key", CommandFlags.None);
        mock.Verify(x => x.KeyTouchAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task key_touch_async_2()
    {
        RedisKey[] keys = ["a", "b"];
        await prefixed.KeyTouchAsync(keys, CommandFlags.None);
        mock.Verify(x => x.KeyTouchAsync(IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }
    [Fact]
    public async Task execute_async_1()
    {
        await prefixed.ExecuteAsync("CUSTOM", "arg1", (RedisKey)"arg2");
        mock.Verify(x => x.ExecuteAsync("CUSTOM", It.Is<object[]>(args => args.Length == 2 && args[0].Equals("arg1") && args[1].Equals((RedisKey)"prefix:arg2")), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task execute_async_2()
    {
        var args = new List<object> { "arg1", (RedisKey)"arg2" };
        await prefixed.ExecuteAsync("CUSTOM", args, CommandFlags.None);
        mock.Verify(x => x.ExecuteAsync("CUSTOM", It.Is<ICollection<object>?>(a => a != null && a.Count == 2 && a.ElementAt(0).Equals("arg1") && a.ElementAt(1).Equals((RedisKey)"prefix:arg2")), CommandFlags.None), Times.AtLeastOnce());
    }
    [Fact]
    public async Task geo_add_async_1()
    {
        await prefixed.GeoAddAsync("key", 1.23, 4.56, "member", CommandFlags.None);
        mock.Verify(x => x.GeoAddAsync("prefix:key", 1.23, 4.56, "member", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task geo_add_async_2()
    {
        var geoEntry = new GeoEntry(1.23, 4.56, "member");
        await prefixed.GeoAddAsync("key", geoEntry, CommandFlags.None);
        mock.Verify(x => x.GeoAddAsync("prefix:key", geoEntry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task geo_add_async_3()
    {
        var geoEntries = new GeoEntry[] { new GeoEntry(1.23, 4.56, "member1") };
        await prefixed.GeoAddAsync("key", geoEntries, CommandFlags.None);
        mock.Verify(x => x.GeoAddAsync("prefix:key", geoEntries, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task geo_remove_async()
    {
        await prefixed.GeoRemoveAsync("key", "member", CommandFlags.None);
        mock.Verify(x => x.GeoRemoveAsync("prefix:key", "member", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task geo_distance_async()
    {
        await prefixed.GeoDistanceAsync("key", "member1", "member2", GeoUnit.Meters, CommandFlags.None);
        mock.Verify(x => x.GeoDistanceAsync("prefix:key", "member1", "member2", GeoUnit.Meters, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task geo_hash_async_1()
    {
        await prefixed.GeoHashAsync("key", "member", CommandFlags.None);
        mock.Verify(x => x.GeoHashAsync("prefix:key", "member", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task geo_hash_async_2()
    {
        var members = new RedisValue[] { "member1", "member2" };
        await prefixed.GeoHashAsync("key", members, CommandFlags.None);
        mock.Verify(x => x.GeoHashAsync("prefix:key", members, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task geo_position_async_1()
    {
        await prefixed.GeoPositionAsync("key", "member", CommandFlags.None);
        mock.Verify(x => x.GeoPositionAsync("prefix:key", "member", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task geo_position_async_2()
    {
        var members = new RedisValue[] { "member1", "member2" };
        await prefixed.GeoPositionAsync("key", members, CommandFlags.None);
        mock.Verify(x => x.GeoPositionAsync("prefix:key", members, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task geo_radius_async_1()
    {
        await prefixed.GeoRadiusAsync("key", "member", 100, GeoUnit.Meters, 10, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None);
        mock.Verify(x => x.GeoRadiusAsync("prefix:key", "member", 100, GeoUnit.Meters, 10, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task geo_radius_async_2()
    {
        await prefixed.GeoRadiusAsync("key", 1.23, 4.56, 100, GeoUnit.Meters, 10, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None);
        mock.Verify(x => x.GeoRadiusAsync("prefix:key", 1.23, 4.56, 100, GeoUnit.Meters, 10, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task geo_search_async_1()
    {
        var shape = new GeoSearchCircle(100, GeoUnit.Meters);
        await prefixed.GeoSearchAsync("key", "member", shape, 10, true, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None);
        mock.Verify(x => x.GeoSearchAsync("prefix:key", "member", shape, 10, true, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task geo_search_async_2()
    {
        var shape = new GeoSearchCircle(100, GeoUnit.Meters);
        await prefixed.GeoSearchAsync("key", 1.23, 4.56, shape, 10, true, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None);
        mock.Verify(x => x.GeoSearchAsync("prefix:key", 1.23, 4.56, shape, 10, true, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task geo_search_and_store_async_1()
    {
        var shape = new GeoSearchCircle(100, GeoUnit.Meters);
        await prefixed.GeoSearchAndStoreAsync("source", "destination", "member", shape, 10, true, Order.Ascending, false, CommandFlags.None);
        mock.Verify(x => x.GeoSearchAndStoreAsync("prefix:source", "prefix:destination", "member", shape, 10, true, Order.Ascending, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task geo_search_and_store_async_2()
    {
        var shape = new GeoSearchCircle(100, GeoUnit.Meters);
        await prefixed.GeoSearchAndStoreAsync("source", "destination", 1.23, 4.56, shape, 10, true, Order.Ascending, false, CommandFlags.None);
        mock.Verify(x => x.GeoSearchAndStoreAsync("prefix:source", "prefix:destination", 1.23, 4.56, shape, 10, true, Order.Ascending, false, CommandFlags.None), Times.AtLeastOnce());
    }
    [Fact]
    public async Task hash_field_expire_async_1()
    {
        var hashFields = new RedisValue[] { "field1", "field2" };
        var expiry = TimeSpan.FromSeconds(60);
        await prefixed.HashFieldExpireAsync("key", hashFields, expiry, ExpireWhen.Always, CommandFlags.None);
        mock.Verify(x => x.HashFieldExpireAsync("prefix:key", hashFields, expiry, ExpireWhen.Always, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_field_expire_async_2()
    {
        var hashFields = new RedisValue[] { "field1", "field2" };
        var expiry = DateTime.Now.AddMinutes(1);
        await prefixed.HashFieldExpireAsync("key", hashFields, expiry, ExpireWhen.Always, CommandFlags.None);
        mock.Verify(x => x.HashFieldExpireAsync("prefix:key", hashFields, expiry, ExpireWhen.Always, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_field_get_expire_date_time_async()
    {
        var hashFields = new RedisValue[] { "field1", "field2" };
        await prefixed.HashFieldGetExpireDateTimeAsync("key", hashFields, CommandFlags.None);
        mock.Verify(x => x.HashFieldGetExpireDateTimeAsync("prefix:key", hashFields, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_field_persist_async()
    {
        var hashFields = new RedisValue[] { "field1", "field2" };
        await prefixed.HashFieldPersistAsync("key", hashFields, CommandFlags.None);
        mock.Verify(x => x.HashFieldPersistAsync("prefix:key", hashFields, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_field_get_time_to_live_async()
    {
        var hashFields = new RedisValue[] { "field1", "field2" };
        await prefixed.HashFieldGetTimeToLiveAsync("key", hashFields, CommandFlags.None);
        mock.Verify(x => x.HashFieldGetTimeToLiveAsync("prefix:key", hashFields, CommandFlags.None), Times.AtLeastOnce());
    }
    [Fact]
    public async Task hash_get_lease_async()
    {
        await prefixed.HashGetLeaseAsync("key", "field", CommandFlags.None);
        mock.Verify(x => x.HashGetLeaseAsync("prefix:key", "field", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_field_get_and_delete_async_1()
    {
        await prefixed.HashFieldGetAndDeleteAsync("key", "field", CommandFlags.None);
        mock.Verify(x => x.HashFieldGetAndDeleteAsync("prefix:key", "field", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_field_get_and_delete_async_2()
    {
        var hashFields = new RedisValue[] { "field1", "field2" };
        await prefixed.HashFieldGetAndDeleteAsync("key", hashFields, CommandFlags.None);
        mock.Verify(x => x.HashFieldGetAndDeleteAsync("prefix:key", hashFields, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_field_get_lease_and_delete_async()
    {
        await prefixed.HashFieldGetLeaseAndDeleteAsync("key", "field", CommandFlags.None);
        mock.Verify(x => x.HashFieldGetLeaseAndDeleteAsync("prefix:key", "field", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_field_get_and_set_expiry_async_1()
    {
        var expiry = TimeSpan.FromMinutes(5);
        await prefixed.HashFieldGetAndSetExpiryAsync("key", "field", expiry, false, CommandFlags.None);
        mock.Verify(x => x.HashFieldGetAndSetExpiryAsync("prefix:key", "field", expiry, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_field_get_and_set_expiry_async_2()
    {
        var expiry = DateTime.Now.AddMinutes(5);
        await prefixed.HashFieldGetAndSetExpiryAsync("key", "field", expiry, CommandFlags.None);
        mock.Verify(x => x.HashFieldGetAndSetExpiryAsync("prefix:key", "field", expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_field_get_lease_and_set_expiry_async_1()
    {
        var expiry = TimeSpan.FromMinutes(5);
        await prefixed.HashFieldGetLeaseAndSetExpiryAsync("key", "field", expiry, false, CommandFlags.None);
        mock.Verify(x => x.HashFieldGetLeaseAndSetExpiryAsync("prefix:key", "field", expiry, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task hash_field_get_lease_and_set_expiry_async_2()
    {
        var expiry = DateTime.Now.AddMinutes(5);
        await prefixed.HashFieldGetLeaseAndSetExpiryAsync("key", "field", expiry, CommandFlags.None);
        mock.Verify(x => x.HashFieldGetLeaseAndSetExpiryAsync("prefix:key", "field", expiry, CommandFlags.None), Times.AtLeastOnce());
    }
    [Fact]
    public async Task string_get_lease_async()
    {
        await prefixed.StringGetLeaseAsync("key", CommandFlags.None);
        mock.Verify(x => x.StringGetLeaseAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_get_set_expiry_async_1()
    {
        var expiry = TimeSpan.FromMinutes(5);
        await prefixed.StringGetSetExpiryAsync("key", expiry, CommandFlags.None);
        mock.Verify(x => x.StringGetSetExpiryAsync("prefix:key", expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_get_set_expiry_async_2()
    {
        var expiry = DateTime.Now.AddMinutes(5);
        await prefixed.StringGetSetExpiryAsync("key", expiry, CommandFlags.None);
        mock.Verify(x => x.StringGetSetExpiryAsync("prefix:key", expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_set_and_get_async_1()
    {
        var expiry = TimeSpan.FromMinutes(5);
        await prefixed.StringSetAndGetAsync("key", "value", expiry, When.Always, CommandFlags.None);
        mock.Verify(x => x.StringSetAndGetAsync("prefix:key", "value", expiry, When.Always, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_set_and_get_async_2()
    {
        var expiry = TimeSpan.FromMinutes(5);
        await prefixed.StringSetAndGetAsync("key", "value", expiry, false, When.Always, CommandFlags.None);
        mock.Verify(x => x.StringSetAndGetAsync("prefix:key", "value", expiry, false, When.Always, CommandFlags.None), Times.AtLeastOnce());
    }
    [Fact]
    public async Task string_longest_common_subsequence_async()
    {
        await prefixed.StringLongestCommonSubsequenceAsync("key1", "key2", CommandFlags.None);
        mock.Verify(x => x.StringLongestCommonSubsequenceAsync("prefix:key1", "prefix:key2", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_longest_common_subsequence_length_async()
    {
        await prefixed.StringLongestCommonSubsequenceLengthAsync("key1", "key2", CommandFlags.None);
        mock.Verify(x => x.StringLongestCommonSubsequenceLengthAsync("prefix:key1", "prefix:key2", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task string_longest_common_subsequence_with_matches_async()
    {
        await prefixed.StringLongestCommonSubsequenceWithMatchesAsync("key1", "key2", 5, CommandFlags.None);
        mock.Verify(x => x.StringLongestCommonSubsequenceWithMatchesAsync("prefix:key1", "prefix:key2", 5, CommandFlags.None), Times.AtLeastOnce());
    }
    [Fact]
    public async Task key_idle_time_async()
    {
        await prefixed.KeyIdleTimeAsync("key", CommandFlags.None);
        mock.Verify(x => x.KeyIdleTimeAsync("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }
    [Fact]
    public async Task stream_add_async_with_trim_mode_1()
    {
        await prefixed.StreamAddAsync("key", "field", "value", "*", 1000, false, 100, StreamTrimMode.KeepReferences, CommandFlags.None);
        mock.Verify(x => x.StreamAddAsync("prefix:key", "field", "value", "*", 1000, false, 100, StreamTrimMode.KeepReferences, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_add_async_with_trim_mode_2()
    {
        var fields = new NameValueEntry[] { new NameValueEntry("field", "value") };
        await prefixed.StreamAddAsync("key", fields, "*", 1000, false, 100, StreamTrimMode.KeepReferences, CommandFlags.None);
        mock.Verify(x => x.StreamAddAsync("prefix:key", fields, "*", 1000, false, 100, StreamTrimMode.KeepReferences, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_add_async_with_options_1()
    {
        var options = new StreamAddOptions { MaxLength = 1000, CreateStream = false };
        await prefixed.StreamAddAsync("key", "field", "value", options, CommandFlags.None);
        mock.Verify(x => x.StreamAddAsync("prefix:key", "field", "value", options, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_add_async_with_options_2()
    {
        var fields = new NameValueEntry[] { new NameValueEntry("field", "value") };
        var options = new StreamAddOptions { MinId = "5-5", CreateStream = false };
        await prefixed.StreamAddAsync("key", fields, options, CommandFlags.None);
        mock.Verify(x => x.StreamAddAsync("prefix:key", fields, options, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_trim_async_with_mode()
    {
        await prefixed.StreamTrimAsync("key", 1000, false, 100, StreamTrimMode.KeepReferences, CommandFlags.None);
        mock.Verify(x => x.StreamTrimAsync("prefix:key", 1000, false, 100, StreamTrimMode.KeepReferences, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_trim_by_min_id_async_with_mode()
    {
        await prefixed.StreamTrimByMinIdAsync("key", "1111111111", false, 100, StreamTrimMode.KeepReferences, CommandFlags.None);
        mock.Verify(x => x.StreamTrimByMinIdAsync("prefix:key", "1111111111", false, 100, StreamTrimMode.KeepReferences, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_read_group_async_with_no_ack_1()
    {
        await prefixed.StreamReadGroupAsync("key", "group", "consumer", "0-0", 10, true, CommandFlags.None);
        mock.Verify(x => x.StreamReadGroupAsync("prefix:key", "group", "consumer", "0-0", 10, true, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_read_group_async_with_no_ack_2()
    {
        var streamPositions = new StreamPosition[] { new StreamPosition("key", "0-0") };
        await prefixed.StreamReadGroupAsync(streamPositions, "group", "consumer", 10, true, CommandFlags.None);
        mock.Verify(x => x.StreamReadGroupAsync(streamPositions, "group", "consumer", 10, true, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_trim_async_simple()
    {
        await prefixed.StreamTrimAsync("key", 1000, true, CommandFlags.None);
        mock.Verify(x => x.StreamTrimAsync("prefix:key", 1000, true, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_read_group_async_simple_1()
    {
        await prefixed.StreamReadGroupAsync("key", "group", "consumer", "0-0", 10, CommandFlags.None);
        mock.Verify(x => x.StreamReadGroupAsync("prefix:key", "group", "consumer", "0-0", 10, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public async Task stream_read_group_async_simple_2()
    {
        var streamPositions = new StreamPosition[] { new StreamPosition("key", "0-0") };
        await prefixed.StreamReadGroupAsync(streamPositions, "group", "consumer", 10, CommandFlags.None);
        mock.Verify(x => x.StreamReadGroupAsync(streamPositions, "group", "consumer", 10, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_scan_async()
    {
        var result = prefixed.HashScanAsync("key", "pattern*", 10, 1, 2, CommandFlags.None);
        mock.Verify(x => x.HashScanAsync("prefix:key", "pattern*", 10, 1, 2, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_scan_no_values_async()
    {
        var result = prefixed.HashScanNoValuesAsync("key", "pattern*", 10, 1, 2, CommandFlags.None);
        mock.Verify(x => x.HashScanNoValuesAsync("prefix:key", "pattern*", 10, 1, 2, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_scan_async()
    {
        var result = prefixed.SetScanAsync("key", "pattern*", 10, 1, 2, CommandFlags.None);
        mock.Verify(x => x.SetScanAsync("prefix:key", "pattern*", 10, 1, 2, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_scan_async()
    {
        var result = prefixed.SortedSetScanAsync("key", "pattern*", 10, 1, 2, CommandFlags.None);
        mock.Verify(x => x.SortedSetScanAsync("prefix:key", "pattern*", 10, 1, 2, CommandFlags.None), Times.AtLeastOnce());
    }
}

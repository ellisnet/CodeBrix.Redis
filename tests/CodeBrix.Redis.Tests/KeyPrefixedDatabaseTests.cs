using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using CodeBrix.Redis.KeyspaceIsolation;
using CodeBrix.TestMocks.Mocking;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[CollectionDefinition(nameof(SubstituteDependentCollection), DisableParallelization = true)]
public class SubstituteDependentCollection { }

[Collection(nameof(SubstituteDependentCollection))]
public sealed class KeyPrefixedDatabaseTests
{
    private readonly Mock<IDatabase> mock;
    private readonly IDatabase prefixed;

    internal static RedisKey[] IsKeys(params RedisKey[] expected) => IsRaw(expected);
    internal static RedisValue[] IsValues(params RedisValue[] expected) => IsRaw(expected);
    //NSubstitute's Arg.Is(...) can be returned from a helper and used in argument position;
    //CodeBrix.TestMocks' It.Is<T>(...) is only legal directly inside a Setup/Verify expression
    //tree, so the reusable matcher is built with Match.Create, which registers the same predicate
    //and may be called from a helper. The 18 IsKeys/IsValues call sites are unchanged.
    private static T[] IsRaw<T>(T[] expected)
        => Match.Create<T[]>(actual => actual.Length == expected.Length && expected.SequenceEqual(actual));

    public KeyPrefixedDatabaseTests()
    {
        mock = new Mock<IDatabase>();
        prefixed = new KeyPrefixedDatabase(mock.Object, Encoding.UTF8.GetBytes("prefix:"));
    }

    [Fact]
    public void create_batch()
    {
        object asyncState = new();
        IBatch innerBatch = new Mock<IBatch>().Object;
        mock.Setup(x => x.CreateBatch(asyncState)).Returns(innerBatch);
        IBatch wrappedBatch = prefixed.CreateBatch(asyncState);
        mock.Verify(x => x.CreateBatch(asyncState), Times.AtLeastOnce());
        wrappedBatch.Should().BeOfType<KeyPrefixedBatch>();
        (((KeyPrefixedBatch)wrappedBatch).Inner).Should().BeSameAs(innerBatch);
    }

    [Fact]
    public void create_transaction()
    {
        object asyncState = new();
        ITransaction innerTransaction = new Mock<ITransaction>().Object;
        mock.Setup(x => x.CreateTransaction(asyncState)).Returns(innerTransaction);
        ITransaction wrappedTransaction = prefixed.CreateTransaction(asyncState);
        mock.Verify(x => x.CreateTransaction(asyncState), Times.AtLeastOnce());
        wrappedTransaction.Should().BeOfType<KeyPrefixedTransaction>();
        (((KeyPrefixedTransaction)wrappedTransaction).Inner).Should().BeSameAs(innerTransaction);
    }

    [Fact]
    public void debug_object()
    {
        prefixed.DebugObject("key", CommandFlags.None);
        mock.Verify(x => x.DebugObject("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_decrement_1()
    {
        prefixed.HashDecrement("key", "hashField", 123, CommandFlags.None);
        mock.Verify(x => x.HashDecrement("prefix:key", "hashField", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_decrement_2()
    {
        prefixed.HashDecrement("key", "hashField", 1.23, CommandFlags.None);
        mock.Verify(x => x.HashDecrement("prefix:key", "hashField", 1.23, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_delete_1()
    {
        prefixed.HashDelete("key", "hashField", CommandFlags.None);
        mock.Verify(x => x.HashDelete("prefix:key", "hashField", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_delete_2()
    {
        RedisValue[] hashFields = Array.Empty<RedisValue>();
        prefixed.HashDelete("key", hashFields, CommandFlags.None);
        mock.Verify(x => x.HashDelete("prefix:key", hashFields, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_exists()
    {
        prefixed.HashExists("key", "hashField", CommandFlags.None);
        mock.Verify(x => x.HashExists("prefix:key", "hashField", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_get_1()
    {
        prefixed.HashGet("key", "hashField", CommandFlags.None);
        mock.Verify(x => x.HashGet("prefix:key", "hashField", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_get_2()
    {
        RedisValue[] hashFields = Array.Empty<RedisValue>();
        prefixed.HashGet("key", hashFields, CommandFlags.None);
        mock.Verify(x => x.HashGet("prefix:key", hashFields, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_get_all()
    {
        prefixed.HashGetAll("key", CommandFlags.None);
        mock.Verify(x => x.HashGetAll("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_increment_1()
    {
        prefixed.HashIncrement("key", "hashField", 123, CommandFlags.None);
        mock.Verify(x => x.HashIncrement("prefix:key", "hashField", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_increment_2()
    {
        prefixed.HashIncrement("key", "hashField", 1.23, CommandFlags.None);
        mock.Verify(x => x.HashIncrement("prefix:key", "hashField", 1.23, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_keys()
    {
        prefixed.HashKeys("key", CommandFlags.None);
        mock.Verify(x => x.HashKeys("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_length()
    {
        prefixed.HashLength("key", CommandFlags.None);
        mock.Verify(x => x.HashLength("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_scan()
    {
        prefixed.HashScan("key", "pattern", 123, flags: CommandFlags.None);
        mock.Verify(x => x.HashScan("prefix:key", "pattern", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_scan_full()
    {
        prefixed.HashScan("key", "pattern", 123, 42, 64, flags: CommandFlags.None);
        mock.Verify(x => x.HashScan("prefix:key", "pattern", 123, 42, 64, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_scan_no_values()
    {
        prefixed.HashScanNoValues("key", "pattern", 123, flags: CommandFlags.None);
        mock.Verify(x => x.HashScanNoValues("prefix:key", "pattern", 123, RedisBase.CursorUtils.Origin, 0, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_scan_no_values_full()
    {
        prefixed.HashScanNoValues("key", "pattern", 123, 42, 64, flags: CommandFlags.None);
        mock.Verify(x => x.HashScanNoValues("prefix:key", "pattern", 123, 42, 64, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_set_1()
    {
        HashEntry[] hashFields = Array.Empty<HashEntry>();
        prefixed.HashSet("key", hashFields, CommandFlags.None);
        mock.Verify(x => x.HashSet("prefix:key", hashFields, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_set_2()
    {
        prefixed.HashSet("key", "hashField", "value", When.Exists, CommandFlags.None);
        mock.Verify(x => x.HashSet("prefix:key", "hashField", "value", When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_string_length()
    {
        prefixed.HashStringLength("key", "field", CommandFlags.None);
        mock.Verify(x => x.HashStringLength("prefix:key", "field", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_values()
    {
        prefixed.HashValues("key", CommandFlags.None);
        mock.Verify(x => x.HashValues("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hyper_log_log_add_1()
    {
        prefixed.HyperLogLogAdd("key", "value", CommandFlags.None);
        mock.Verify(x => x.HyperLogLogAdd("prefix:key", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hyper_log_log_add_2()
    {
        RedisValue[] values = Array.Empty<RedisValue>();
        prefixed.HyperLogLogAdd("key", values, CommandFlags.None);
        mock.Verify(x => x.HyperLogLogAdd("prefix:key", values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hyper_log_log_length()
    {
        prefixed.HyperLogLogLength("key", CommandFlags.None);
        mock.Verify(x => x.HyperLogLogLength("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hyper_log_log_merge_1()
    {
        prefixed.HyperLogLogMerge("destination", "first", "second", CommandFlags.None);
        mock.Verify(x => x.HyperLogLogMerge("prefix:destination", "prefix:first", "prefix:second", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hyper_log_log_merge_2()
    {
        prefixed.HyperLogLogMerge("destination", ["a", "b"], CommandFlags.None);
        mock.Verify(x => x.HyperLogLogMerge("prefix:destination", IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void identify_endpoint()
    {
        prefixed.IdentifyEndpoint("key", CommandFlags.None);
        mock.Verify(x => x.IdentifyEndpoint("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_copy()
    {
        prefixed.KeyCopy("key", "destination", flags: CommandFlags.None);
        mock.Verify(x => x.KeyCopy("prefix:key", "prefix:destination", -1, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_delete_1()
    {
        prefixed.KeyDelete("key", CommandFlags.None);
        mock.Verify(x => x.KeyDelete("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_delete_2()
    {
        prefixed.KeyDelete(["a", "b"], CommandFlags.None);
        mock.Verify(x => x.KeyDelete(IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_dump()
    {
        prefixed.KeyDump("key", CommandFlags.None);
        mock.Verify(x => x.KeyDump("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_encoding()
    {
        prefixed.KeyEncoding("key", CommandFlags.None);
        mock.Verify(x => x.KeyEncoding("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_exists()
    {
        prefixed.KeyExists("key", CommandFlags.None);
        mock.Verify(x => x.KeyExists("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_expire_1()
    {
        TimeSpan expiry = TimeSpan.FromSeconds(123);
        prefixed.KeyExpire("key", expiry, CommandFlags.None);
        mock.Verify(x => x.KeyExpire("prefix:key", expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_expire_2()
    {
        DateTime expiry = DateTime.Now;
        prefixed.KeyExpire("key", expiry, CommandFlags.None);
        mock.Verify(x => x.KeyExpire("prefix:key", expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_expire_3()
    {
        TimeSpan expiry = TimeSpan.FromSeconds(123);
        prefixed.KeyExpire("key", expiry, ExpireWhen.HasNoExpiry, CommandFlags.None);
        mock.Verify(x => x.KeyExpire("prefix:key", expiry, ExpireWhen.HasNoExpiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_expire_4()
    {
        DateTime expiry = DateTime.Now;
        prefixed.KeyExpire("key", expiry, ExpireWhen.HasNoExpiry, CommandFlags.None);
        mock.Verify(x => x.KeyExpire("prefix:key", expiry, ExpireWhen.HasNoExpiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_expire_time()
    {
        prefixed.KeyExpireTime("key", CommandFlags.None);
        mock.Verify(x => x.KeyExpireTime("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_frequency()
    {
        prefixed.KeyFrequency("key", CommandFlags.None);
        mock.Verify(x => x.KeyFrequency("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_migrate()
    {
        EndPoint toServer = new IPEndPoint(IPAddress.Loopback, 123);
        prefixed.KeyMigrate("key", toServer, 123, 456, MigrateOptions.Copy, CommandFlags.None);
        mock.Verify(x => x.KeyMigrate("prefix:key", toServer, 123, 456, MigrateOptions.Copy, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_move()
    {
        prefixed.KeyMove("key", 123, CommandFlags.None);
        mock.Verify(x => x.KeyMove("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_persist()
    {
        prefixed.KeyPersist("key", CommandFlags.None);
        mock.Verify(x => x.KeyPersist("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_random() => Assert.Throws<NotSupportedException>(() => prefixed.KeyRandom());

    [Fact]
    public void key_ref_count()
    {
        prefixed.KeyRefCount("key", CommandFlags.None);
        mock.Verify(x => x.KeyRefCount("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_rename()
    {
        prefixed.KeyRename("key", "newKey", When.Exists, CommandFlags.None);
        mock.Verify(x => x.KeyRename("prefix:key", "prefix:newKey", When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_restore()
    {
        byte[] value = Array.Empty<byte>();
        TimeSpan expiry = TimeSpan.FromSeconds(123);
        prefixed.KeyRestore("key", value, expiry, CommandFlags.None);
        mock.Verify(x => x.KeyRestore("prefix:key", value, expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_time_to_live()
    {
        prefixed.KeyTimeToLive("key", CommandFlags.None);
        mock.Verify(x => x.KeyTimeToLive("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void key_type()
    {
        prefixed.KeyType("key", CommandFlags.None);
        mock.Verify(x => x.KeyType("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_get_by_index()
    {
        prefixed.ListGetByIndex("key", 123, CommandFlags.None);
        mock.Verify(x => x.ListGetByIndex("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_insert_after()
    {
        prefixed.ListInsertAfter("key", "pivot", "value", CommandFlags.None);
        mock.Verify(x => x.ListInsertAfter("prefix:key", "pivot", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_insert_before()
    {
        prefixed.ListInsertBefore("key", "pivot", "value", CommandFlags.None);
        mock.Verify(x => x.ListInsertBefore("prefix:key", "pivot", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_left_pop()
    {
        prefixed.ListLeftPop("key", CommandFlags.None);
        mock.Verify(x => x.ListLeftPop("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_left_pop_1()
    {
        prefixed.ListLeftPop("key", 123, CommandFlags.None);
        mock.Verify(x => x.ListLeftPop("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_left_push_1()
    {
        prefixed.ListLeftPush("key", "value", When.Exists, CommandFlags.None);
        mock.Verify(x => x.ListLeftPush("prefix:key", "value", When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_left_push_2()
    {
        RedisValue[] values = Array.Empty<RedisValue>();
        prefixed.ListLeftPush("key", values, CommandFlags.None);
        mock.Verify(x => x.ListLeftPush("prefix:key", values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_left_push_3()
    {
        RedisValue[] values = ["value1", "value2"];
        prefixed.ListLeftPush("key", values, When.Exists, CommandFlags.None);
        mock.Verify(x => x.ListLeftPush("prefix:key", values, When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_length()
    {
        prefixed.ListLength("key", CommandFlags.None);
        mock.Verify(x => x.ListLength("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_move()
    {
        prefixed.ListMove("key", "destination", ListSide.Left, ListSide.Right, CommandFlags.None);
        mock.Verify(x => x.ListMove("prefix:key", "prefix:destination", ListSide.Left, ListSide.Right, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_range()
    {
        prefixed.ListRange("key", 123, 456, CommandFlags.None);
        mock.Verify(x => x.ListRange("prefix:key", 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_remove()
    {
        prefixed.ListRemove("key", "value", 123, CommandFlags.None);
        mock.Verify(x => x.ListRemove("prefix:key", "value", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_right_pop()
    {
        prefixed.ListRightPop("key", CommandFlags.None);
        mock.Verify(x => x.ListRightPop("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_right_pop_1()
    {
        prefixed.ListRightPop("key", 123, CommandFlags.None);
        mock.Verify(x => x.ListRightPop("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_right_pop_left_push()
    {
        prefixed.ListRightPopLeftPush("source", "destination", CommandFlags.None);
        mock.Verify(x => x.ListRightPopLeftPush("prefix:source", "prefix:destination", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_right_push_1()
    {
        prefixed.ListRightPush("key", "value", When.Exists, CommandFlags.None);
        mock.Verify(x => x.ListRightPush("prefix:key", "value", When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_right_push_2()
    {
        RedisValue[] values = Array.Empty<RedisValue>();
        prefixed.ListRightPush("key", values, CommandFlags.None);
        mock.Verify(x => x.ListRightPush("prefix:key", values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_right_push_3()
    {
        RedisValue[] values = ["value1", "value2"];
        prefixed.ListRightPush("key", values, When.Exists, CommandFlags.None);
        mock.Verify(x => x.ListRightPush("prefix:key", values, When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_set_by_index()
    {
        prefixed.ListSetByIndex("key", 123, "value", CommandFlags.None);
        mock.Verify(x => x.ListSetByIndex("prefix:key", 123, "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void list_trim()
    {
        prefixed.ListTrim("key", 123, 456, CommandFlags.None);
        mock.Verify(x => x.ListTrim("prefix:key", 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void lock_extend()
    {
        TimeSpan expiry = TimeSpan.FromSeconds(123);
        prefixed.LockExtend("key", "value", expiry, CommandFlags.None);
        mock.Verify(x => x.LockExtend("prefix:key", "value", expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void lock_query()
    {
        prefixed.LockQuery("key", CommandFlags.None);
        mock.Verify(x => x.LockQuery("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void lock_release()
    {
        prefixed.LockRelease("key", "value", CommandFlags.None);
        mock.Verify(x => x.LockRelease("prefix:key", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void lock_take()
    {
        TimeSpan expiry = TimeSpan.FromSeconds(123);
        prefixed.LockTake("key", "value", expiry, CommandFlags.None);
        mock.Verify(x => x.LockTake("prefix:key", "value", expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void publish()
    {
        prefixed.Publish(RedisChannel.Literal("channel"), "message", CommandFlags.None);
        mock.Verify(x => x.Publish(RedisChannel.Literal("prefix:channel"), "message", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void script_evaluate_1()
    {
        byte[] hash = Array.Empty<byte>();
        RedisValue[] values = Array.Empty<RedisValue>();
        RedisKey[] keys = ["a", "b"];
        prefixed.ScriptEvaluate(hash, keys, values, CommandFlags.None);
        mock.Verify(x => x.ScriptEvaluate(hash, IsKeys("prefix:a", "prefix:b"), values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void script_evaluate_2()
    {
        RedisValue[] values = Array.Empty<RedisValue>();
        RedisKey[] keys = ["a", "b"];
        prefixed.ScriptEvaluate(script: "script", keys: keys, values: values, flags: CommandFlags.None);
        mock.Verify(x => x.ScriptEvaluate(script: "script", keys: IsKeys("prefix:a", "prefix:b"), values: values, flags: CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_add_1()
    {
        prefixed.SetAdd("key", "value", CommandFlags.None);
        mock.Verify(x => x.SetAdd("prefix:key", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_add_2()
    {
        RedisValue[] values = Array.Empty<RedisValue>();
        prefixed.SetAdd("key", values, CommandFlags.None);
        mock.Verify(x => x.SetAdd("prefix:key", values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_combine_1()
    {
        prefixed.SetCombine(SetOperation.Intersect, "first", "second", CommandFlags.None);
        mock.Verify(x => x.SetCombine(SetOperation.Intersect, "prefix:first", "prefix:second", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_combine_2()
    {
        RedisKey[] keys = ["a", "b"];
        prefixed.SetCombine(SetOperation.Intersect, keys, CommandFlags.None);
        mock.Verify(x => x.SetCombine(SetOperation.Intersect, IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_combine_and_store_1()
    {
        prefixed.SetCombineAndStore(SetOperation.Intersect, "destination", "first", "second", CommandFlags.None);
        mock.Verify(x => x.SetCombineAndStore(SetOperation.Intersect, "prefix:destination", "prefix:first", "prefix:second", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_combine_and_store_2()
    {
        RedisKey[] keys = ["a", "b"];
        prefixed.SetCombineAndStore(SetOperation.Intersect, "destination", keys, CommandFlags.None);
        mock.Verify(x => x.SetCombineAndStore(SetOperation.Intersect, "prefix:destination", IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_contains()
    {
        prefixed.SetContains("key", "value", CommandFlags.None);
        mock.Verify(x => x.SetContains("prefix:key", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_contains_2()
    {
        RedisValue[] values = ["value1", "value2"];
        prefixed.SetContains("key", values, CommandFlags.None);
        mock.Verify(x => x.SetContains("prefix:key", values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_intersection_length()
    {
        prefixed.SetIntersectionLength(["key1", "key2"]);
        mock.Verify(x => x.SetIntersectionLength(IsKeys("prefix:key1", "prefix:key2"), 0, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_combine_length()
    {
        prefixed.SetCombineLength(SetOperation.Union, ["key1", "key2"]);
        mock.Verify(x => x.SetCombineLength(SetOperation.Union, IsKeys("prefix:key1", "prefix:key2"), 0, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_length()
    {
        prefixed.SetLength("key", CommandFlags.None);
        mock.Verify(x => x.SetLength("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_members()
    {
        prefixed.SetMembers("key", CommandFlags.None);
        mock.Verify(x => x.SetMembers("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_move()
    {
        prefixed.SetMove("source", "destination", "value", CommandFlags.None);
        mock.Verify(x => x.SetMove("prefix:source", "prefix:destination", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_pop_1()
    {
        prefixed.SetPop("key", CommandFlags.None);
        mock.Verify(x => x.SetPop("prefix:key", CommandFlags.None), Times.AtLeastOnce());

        prefixed.SetPop("key", 5, CommandFlags.None);
        mock.Verify(x => x.SetPop("prefix:key", 5, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_pop_2()
    {
        prefixed.SetPop("key", 5, CommandFlags.None);
        mock.Verify(x => x.SetPop("prefix:key", 5, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_random_member()
    {
        prefixed.SetRandomMember("key", CommandFlags.None);
        mock.Verify(x => x.SetRandomMember("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_random_members()
    {
        prefixed.SetRandomMembers("key", 123, CommandFlags.None);
        mock.Verify(x => x.SetRandomMembers("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_remove_1()
    {
        prefixed.SetRemove("key", "value", CommandFlags.None);
        mock.Verify(x => x.SetRemove("prefix:key", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_remove_2()
    {
        RedisValue[] values = Array.Empty<RedisValue>();
        prefixed.SetRemove("key", values, CommandFlags.None);
        mock.Verify(x => x.SetRemove("prefix:key", values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_scan()
    {
        prefixed.SetScan("key", "pattern", 123, flags: CommandFlags.None);
        mock.Verify(x => x.SetScan("prefix:key", "pattern", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void set_scan_full()
    {
        prefixed.SetScan("key", "pattern", 123, 42, 64, flags: CommandFlags.None);
        mock.Verify(x => x.SetScan("prefix:key", "pattern", 123, 42, 64, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sort()
    {
        RedisValue[] get = ["a", "#"];

        prefixed.Sort("key", 123, 456, Order.Descending, SortType.Alphabetic, "nosort", get, CommandFlags.None);
        prefixed.Sort("key", 123, 456, Order.Descending, SortType.Alphabetic, "by", get, CommandFlags.None);

        mock.Verify(x => x.Sort("prefix:key", 123, 456, Order.Descending, SortType.Alphabetic, "nosort", IsValues("prefix:a", "#"), CommandFlags.None), Times.AtLeastOnce());
        mock.Verify(x => x.Sort("prefix:key", 123, 456, Order.Descending, SortType.Alphabetic, "prefix:by", IsValues("prefix:a", "#"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sort_and_store()
    {
        RedisValue[] get = ["a", "#"];

        prefixed.SortAndStore("destination", "key", 123, 456, Order.Descending, SortType.Alphabetic, "nosort", get, CommandFlags.None);
        prefixed.SortAndStore("destination", "key", 123, 456, Order.Descending, SortType.Alphabetic, "by", get, CommandFlags.None);

        mock.Verify(x => x.SortAndStore("prefix:destination", "prefix:key", 123, 456, Order.Descending, SortType.Alphabetic, "nosort", IsValues("prefix:a", "#"), CommandFlags.None), Times.AtLeastOnce());
        mock.Verify(x => x.SortAndStore("prefix:destination", "prefix:key", 123, 456, Order.Descending, SortType.Alphabetic, "prefix:by", IsValues("prefix:a", "#"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_add_1()
    {
        prefixed.SortedSetAdd("key", "member", 1.23, When.Exists, CommandFlags.None);
        mock.Verify(x => x.SortedSetAdd("prefix:key", "member", 1.23, When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_add_2()
    {
        SortedSetEntry[] values = Array.Empty<SortedSetEntry>();
        prefixed.SortedSetAdd("key", values, When.Exists, CommandFlags.None);
        mock.Verify(x => x.SortedSetAdd("prefix:key", values, When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_add_3()
    {
        SortedSetEntry[] values = Array.Empty<SortedSetEntry>();
        prefixed.SortedSetAdd("key", values, SortedSetWhen.GreaterThan, CommandFlags.None);
        mock.Verify(x => x.SortedSetAdd("prefix:key", values, SortedSetWhen.GreaterThan, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_combine()
    {
        RedisKey[] keys = ["a", "b"];
        prefixed.SortedSetCombine(SetOperation.Intersect, ["a", "b"]);
        mock.Verify(x => x.SortedSetCombine(SetOperation.Intersect, IsKeys("prefix:a", "prefix:b"), null, Aggregate.Sum, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_combine_with_scores()
    {
        prefixed.SortedSetCombineWithScores(SetOperation.Intersect, ["a", "b"]);
        mock.Verify(x => x.SortedSetCombineWithScores(SetOperation.Intersect, IsKeys("prefix:a", "prefix:b"), null, Aggregate.Sum, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_combine_and_store_1()
    {
        prefixed.SortedSetCombineAndStore(SetOperation.Intersect, "destination", "first", "second", Aggregate.Max, CommandFlags.None);
        mock.Verify(x => x.SortedSetCombineAndStore(SetOperation.Intersect, "prefix:destination", "prefix:first", "prefix:second", Aggregate.Max, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_combine_and_store_2()
    {
        RedisKey[] keys = ["a", "b"];
        prefixed.SetCombineAndStore(SetOperation.Intersect, "destination", keys, CommandFlags.None);
        mock.Verify(x => x.SetCombineAndStore(SetOperation.Intersect, "prefix:destination", IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_decrement()
    {
        prefixed.SortedSetDecrement("key", "member", 1.23, CommandFlags.None);
        mock.Verify(x => x.SortedSetDecrement("prefix:key", "member", 1.23, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_increment()
    {
        prefixed.SortedSetIncrement("key", "member", 1.23, CommandFlags.None);
        mock.Verify(x => x.SortedSetIncrement("prefix:key", "member", 1.23, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_increment_when()
    {
        prefixed.SortedSetIncrement("key", "member", 1.23, ValueCondition.Exists, CommandFlags.None);
        mock.Verify(x => x.SortedSetIncrement("prefix:key", "member", 1.23, ValueCondition.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_intersection_length()
    {
        prefixed.SortedSetIntersectionLength(["a", "b"], 1, CommandFlags.None);
        mock.Verify(x => x.SortedSetIntersectionLength(IsKeys("prefix:a", "prefix:b"), 1, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_length()
    {
        prefixed.SortedSetLength("key", 1.23, 1.23, Exclude.Start, CommandFlags.None);
        mock.Verify(x => x.SortedSetLength("prefix:key", 1.23, 1.23, Exclude.Start, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_random_member()
    {
        prefixed.SortedSetRandomMember("key", CommandFlags.None);
        mock.Verify(x => x.SortedSetRandomMember("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_random_members()
    {
        prefixed.SortedSetRandomMembers("key", 2, CommandFlags.None);
        mock.Verify(x => x.SortedSetRandomMembers("prefix:key", 2, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_random_members_with_scores()
    {
        prefixed.SortedSetRandomMembersWithScores("key", 2, CommandFlags.None);
        mock.Verify(x => x.SortedSetRandomMembersWithScores("prefix:key", 2, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_length_by_value()
    {
        prefixed.SortedSetLengthByValue("key", "min", "max", Exclude.Start, CommandFlags.None);
        mock.Verify(x => x.SortedSetLengthByValue("prefix:key", "min", "max", Exclude.Start, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_range_by_rank()
    {
        prefixed.SortedSetRangeByRank("key", 123, 456, Order.Descending, CommandFlags.None);
        mock.Verify(x => x.SortedSetRangeByRank("prefix:key", 123, 456, Order.Descending, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_range_by_rank_with_scores()
    {
        prefixed.SortedSetRangeByRankWithScores("key", 123, 456, Order.Descending, CommandFlags.None);
        mock.Verify(x => x.SortedSetRangeByRankWithScores("prefix:key", 123, 456, Order.Descending, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_range_by_score()
    {
        prefixed.SortedSetRangeByScore("key", 1.23, 1.23, Exclude.Start, Order.Descending, 123, 456, CommandFlags.None);
        mock.Verify(x => x.SortedSetRangeByScore("prefix:key", 1.23, 1.23, Exclude.Start, Order.Descending, 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_range_by_score_with_scores()
    {
        prefixed.SortedSetRangeByScoreWithScores("key", 1.23, 1.23, Exclude.Start, Order.Descending, 123, 456, CommandFlags.None);
        mock.Verify(x => x.SortedSetRangeByScoreWithScores("prefix:key", 1.23, 1.23, Exclude.Start, Order.Descending, 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_range_by_value()
    {
        prefixed.SortedSetRangeByValue("key", "min", "max", Exclude.Start, 123, 456, CommandFlags.None);
        mock.Verify(x => x.SortedSetRangeByValue("prefix:key", "min", "max", Exclude.Start, Order.Ascending, 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_range_by_value_desc()
    {
        prefixed.SortedSetRangeByValue("key", "min", "max", Exclude.Start, Order.Descending, 123, 456, CommandFlags.None);
        mock.Verify(x => x.SortedSetRangeByValue("prefix:key", "min", "max", Exclude.Start, Order.Descending, 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_rank()
    {
        prefixed.SortedSetRank("key", "member", Order.Descending, CommandFlags.None);
        mock.Verify(x => x.SortedSetRank("prefix:key", "member", Order.Descending, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_remove_1()
    {
        prefixed.SortedSetRemove("key", "member", CommandFlags.None);
        mock.Verify(x => x.SortedSetRemove("prefix:key", "member", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_remove_2()
    {
        RedisValue[] members = Array.Empty<RedisValue>();
        prefixed.SortedSetRemove("key", members, CommandFlags.None);
        mock.Verify(x => x.SortedSetRemove("prefix:key", members, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_remove_range_by_rank()
    {
        prefixed.SortedSetRemoveRangeByRank("key", 123, 456, CommandFlags.None);
        mock.Verify(x => x.SortedSetRemoveRangeByRank("prefix:key", 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_remove_range_by_score()
    {
        prefixed.SortedSetRemoveRangeByScore("key", 1.23, 1.23, Exclude.Start, CommandFlags.None);
        mock.Verify(x => x.SortedSetRemoveRangeByScore("prefix:key", 1.23, 1.23, Exclude.Start, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_remove_range_by_value()
    {
        prefixed.SortedSetRemoveRangeByValue("key", "min", "max", Exclude.Start, CommandFlags.None);
        mock.Verify(x => x.SortedSetRemoveRangeByValue("prefix:key", "min", "max", Exclude.Start, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_scan()
    {
        prefixed.SortedSetScan("key", "pattern", 123, flags: CommandFlags.None);
        mock.Verify(x => x.SortedSetScan("prefix:key", "pattern", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_scan_full()
    {
        prefixed.SortedSetScan("key", "pattern", 123, 42, 64, flags: CommandFlags.None);
        mock.Verify(x => x.SortedSetScan("prefix:key", "pattern", 123, 42, 64, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_score()
    {
        prefixed.SortedSetScore("key", "member", CommandFlags.None);
        mock.Verify(x => x.SortedSetScore("prefix:key", "member", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_score_multiple()
    {
        var values = new RedisValue[] { "member1", "member2" };
        prefixed.SortedSetScores("key", values, CommandFlags.None);
        mock.Verify(x => x.SortedSetScores("prefix:key", values, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void sorted_set_update()
    {
        SortedSetEntry[] values = Array.Empty<SortedSetEntry>();
        prefixed.SortedSetUpdate("key", values, SortedSetWhen.GreaterThan, CommandFlags.None);
        mock.Verify(x => x.SortedSetUpdate("prefix:key", values, SortedSetWhen.GreaterThan, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_acknowledge_1()
    {
        prefixed.StreamAcknowledge("key", "group", "0-0", CommandFlags.None);
        mock.Verify(x => x.StreamAcknowledge("prefix:key", "group", "0-0", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_acknowledge_2()
    {
        var messageIds = new RedisValue[] { "0-0", "0-1", "0-2" };
        prefixed.StreamAcknowledge("key", "group", messageIds, CommandFlags.None);
        mock.Verify(x => x.StreamAcknowledge("prefix:key", "group", messageIds, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_negative_acknowledge_1()
    {
        prefixed.StreamNegativeAcknowledge("key", "group", StreamNackMode.Fail, "0-0", CommandFlags.None);
        mock.Verify(x => x.StreamNegativeAcknowledge("prefix:key", "group", StreamNackMode.Fail, "0-0", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_negative_acknowledge_2()
    {
        var messageIds = new RedisValue[] { "0-0", "0-1", "0-2" };
        prefixed.StreamNegativeAcknowledge("key", "group", StreamNackMode.Fail, messageIds, CommandFlags.None);
        mock.Verify(x => x.StreamNegativeAcknowledge("prefix:key", "group", StreamNackMode.Fail, messageIds, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_add_1()
    {
        prefixed.StreamAdd("key", "field1", "value1", "*", 1000, true, CommandFlags.None);
        mock.Verify(x => x.StreamAdd("prefix:key", "field1", "value1", "*", 1000, true, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_add_2()
    {
        var fields = Array.Empty<NameValueEntry>();
        prefixed.StreamAdd("key", fields, "*", 1000, true, CommandFlags.None);
        mock.Verify(x => x.StreamAdd("prefix:key", fields, "*", 1000, true, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_auto_claim()
    {
        prefixed.StreamAutoClaim("key", "group", "consumer", 0, "0-0", 100, CommandFlags.None);
        mock.Verify(x => x.StreamAutoClaim("prefix:key", "group", "consumer", 0, "0-0", 100, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_auto_claim_ids_only()
    {
        prefixed.StreamAutoClaimIdsOnly("key", "group", "consumer", 0, "0-0", 100, CommandFlags.None);
        mock.Verify(x => x.StreamAutoClaimIdsOnly("prefix:key", "group", "consumer", 0, "0-0", 100, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_claim_messages()
    {
        var messageIds = Array.Empty<RedisValue>();
        prefixed.StreamClaim("key", "group", "consumer", 1000, messageIds, CommandFlags.None);
        mock.Verify(x => x.StreamClaim("prefix:key", "group", "consumer", 1000, messageIds, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_claim_messages_returning_ids()
    {
        var messageIds = Array.Empty<RedisValue>();
        prefixed.StreamClaimIdsOnly("key", "group", "consumer", 1000, messageIds, CommandFlags.None);
        mock.Verify(x => x.StreamClaimIdsOnly("prefix:key", "group", "consumer", 1000, messageIds, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_consumer_group_set_position()
    {
        prefixed.StreamConsumerGroupSetPosition("key", "group", StreamPosition.Beginning, CommandFlags.None);
        mock.Verify(x => x.StreamConsumerGroupSetPosition("prefix:key", "group", StreamPosition.Beginning, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_consumer_info_get()
    {
        prefixed.StreamConsumerInfo("key", "group", CommandFlags.None);
        mock.Verify(x => x.StreamConsumerInfo("prefix:key", "group", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_create_consumer_group()
    {
        prefixed.StreamCreateConsumerGroup("key", "group", StreamPosition.Beginning, false, CommandFlags.None);
        mock.Verify(x => x.StreamCreateConsumerGroup("prefix:key", "group", StreamPosition.Beginning, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_group_info_get()
    {
        prefixed.StreamGroupInfo("key", CommandFlags.None);
        mock.Verify(x => x.StreamGroupInfo("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_info_get()
    {
        prefixed.StreamInfo("key", CommandFlags.None);
        mock.Verify(x => x.StreamInfo("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_length()
    {
        prefixed.StreamLength("key", CommandFlags.None);
        mock.Verify(x => x.StreamLength("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_messages_delete()
    {
        var messageIds = Array.Empty<RedisValue>();
        prefixed.StreamDelete("key", messageIds, CommandFlags.None);
        mock.Verify(x => x.StreamDelete("prefix:key", messageIds, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_delete_consumer()
    {
        prefixed.StreamDeleteConsumer("key", "group", "consumer", CommandFlags.None);
        mock.Verify(x => x.StreamDeleteConsumer("prefix:key", "group", "consumer", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_delete_consumer_group()
    {
        prefixed.StreamDeleteConsumerGroup("key", "group", CommandFlags.None);
        mock.Verify(x => x.StreamDeleteConsumerGroup("prefix:key", "group", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_pending_info_get()
    {
        prefixed.StreamPending("key", "group", CommandFlags.None);
        mock.Verify(x => x.StreamPending("prefix:key", "group", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_pending_message_info_get()
    {
        prefixed.StreamPendingMessages("key", "group", 10, RedisValue.Null, "-", "+", 1000, CommandFlags.None);
        mock.Verify(x => x.StreamPendingMessages("prefix:key", "group", 10, RedisValue.Null, "-", "+", 1000, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_range()
    {
        prefixed.StreamRange("key", "-", "+", null, Order.Ascending, CommandFlags.None);
        mock.Verify(x => x.StreamRange("prefix:key", "-", "+", null, Order.Ascending, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_read_1()
    {
        var streamPositions = Array.Empty<StreamPosition>();
        prefixed.StreamRead(streamPositions, null, CommandFlags.None);
        mock.Verify(x => x.StreamRead(streamPositions, null, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_read_2()
    {
        prefixed.StreamRead("key", "0-0", null, CommandFlags.None);
        mock.Verify(x => x.StreamRead("prefix:key", "0-0", null, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_stream_read_group_1()
    {
        prefixed.StreamReadGroup("key", "group", "consumer", "0-0", 10, false, CommandFlags.None);
        mock.Verify(x => x.StreamReadGroup("prefix:key", "group", "consumer", "0-0", 10, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_stream_read_group_2()
    {
        var streamPositions = Array.Empty<StreamPosition>();
        prefixed.StreamReadGroup(streamPositions, "group", "consumer", 10, false, CommandFlags.None);
        mock.Verify(x => x.StreamReadGroup(streamPositions, "group", "consumer", 10, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_trim()
    {
        prefixed.StreamTrim("key", 1000, true, CommandFlags.None);
        mock.Verify(x => x.StreamTrim("prefix:key", 1000, true, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_trim_by_min_id()
    {
        prefixed.StreamTrimByMinId("key", 1111111111);
        mock.Verify(x => x.StreamTrimByMinId("prefix:key", 1111111111), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_trim_by_min_id_with_approximate()
    {
        prefixed.StreamTrimByMinId("key", 1111111111, useApproximateMaxLength: true);
        mock.Verify(x => x.StreamTrimByMinId("prefix:key", 1111111111, useApproximateMaxLength: true), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_trim_by_min_id_with_approximate_and_limit()
    {
        prefixed.StreamTrimByMinId("key", 1111111111, useApproximateMaxLength: true, limit: 100);
        mock.Verify(x => x.StreamTrimByMinId("prefix:key", 1111111111, useApproximateMaxLength: true, limit: 100), Times.AtLeastOnce());
    }

    [Fact]
    public void string_append()
    {
        prefixed.StringAppend("key", "value", CommandFlags.None);
        mock.Verify(x => x.StringAppend("prefix:key", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_bit_count()
    {
        prefixed.StringBitCount("key", 123, 456, CommandFlags.None);
        mock.Verify(x => x.StringBitCount("prefix:key", 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_bit_count_2()
    {
        prefixed.StringBitCount("key", 123, 456, StringIndexType.Byte, CommandFlags.None);
        mock.Verify(x => x.StringBitCount("prefix:key", 123, 456, StringIndexType.Byte, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_bit_operation_1()
    {
        prefixed.StringBitOperation(Bitwise.Xor, "destination", "first", "second", CommandFlags.None);
        mock.Verify(x => x.StringBitOperation(Bitwise.Xor, "prefix:destination", "prefix:first", "prefix:second", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_bit_operation_2()
    {
        RedisKey[] keys = ["a", "b"];
        prefixed.StringBitOperation(Bitwise.Xor, "destination", keys, CommandFlags.None);
        mock.Verify(x => x.StringBitOperation(Bitwise.Xor, "prefix:destination", IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_bit_operation_diff()
    {
        RedisKey[] keys = ["x", "y1", "y2"];
        prefixed.StringBitOperation(Bitwise.Diff, "destination", keys, CommandFlags.None);
        mock.Verify(x => x.StringBitOperation(Bitwise.Diff, "prefix:destination", IsKeys("prefix:x", "prefix:y1", "prefix:y2"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_bit_operation_diff1()
    {
        RedisKey[] keys = ["x", "y1", "y2"];
        prefixed.StringBitOperation(Bitwise.Diff1, "destination", keys, CommandFlags.None);
        mock.Verify(x => x.StringBitOperation(Bitwise.Diff1, "prefix:destination", IsKeys("prefix:x", "prefix:y1", "prefix:y2"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_bit_operation_and_or()
    {
        RedisKey[] keys = ["x", "y1", "y2"];
        prefixed.StringBitOperation(Bitwise.AndOr, "destination", keys, CommandFlags.None);
        mock.Verify(x => x.StringBitOperation(Bitwise.AndOr, "prefix:destination", IsKeys("prefix:x", "prefix:y1", "prefix:y2"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_bit_operation_one()
    {
        RedisKey[] keys = ["a", "b", "c"];
        prefixed.StringBitOperation(Bitwise.One, "destination", keys, CommandFlags.None);
        mock.Verify(x => x.StringBitOperation(Bitwise.One, "prefix:destination", IsKeys("prefix:a", "prefix:b", "prefix:c"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_bit_position()
    {
        prefixed.StringBitPosition("key", true, 123, 456, CommandFlags.None);
        mock.Verify(x => x.StringBitPosition("prefix:key", true, 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_bit_position_2()
    {
        prefixed.StringBitPosition("key", true, 123, 456, StringIndexType.Byte, CommandFlags.None);
        mock.Verify(x => x.StringBitPosition("prefix:key", true, 123, 456, StringIndexType.Byte, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_decrement_1()
    {
        prefixed.StringDecrement("key", 123, CommandFlags.None);
        mock.Verify(x => x.StringDecrement("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_decrement_2()
    {
        prefixed.StringDecrement("key", 1.23, CommandFlags.None);
        mock.Verify(x => x.StringDecrement("prefix:key", 1.23, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_get_1()
    {
        prefixed.StringGet("key", CommandFlags.None);
        mock.Verify(x => x.StringGet("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_get_2()
    {
        RedisKey[] keys = ["a", "b"];
        prefixed.StringGet(keys, CommandFlags.None);
        mock.Verify(x => x.StringGet(IsKeys("prefix:a", "prefix:b"), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_get_bit()
    {
        prefixed.StringGetBit("key", 123, CommandFlags.None);
        mock.Verify(x => x.StringGetBit("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_get_range()
    {
        prefixed.StringGetRange("key", 123, 456, CommandFlags.None);
        mock.Verify(x => x.StringGetRange("prefix:key", 123, 456, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_get_set()
    {
        prefixed.StringGetSet("key", "value", CommandFlags.None);
        mock.Verify(x => x.StringGetSet("prefix:key", "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_get_delete()
    {
        prefixed.StringGetDelete("key", CommandFlags.None);
        mock.Verify(x => x.StringGetDelete("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_get_with_expiry()
    {
        prefixed.StringGetWithExpiry("key", CommandFlags.None);
        mock.Verify(x => x.StringGetWithExpiry("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_increment_1()
    {
        prefixed.StringIncrement("key", 123, CommandFlags.None);
        mock.Verify(x => x.StringIncrement("prefix:key", 123, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_increment_2()
    {
        prefixed.StringIncrement("key", 1.23, CommandFlags.None);
        mock.Verify(x => x.StringIncrement("prefix:key", 1.23, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_increment_3()
    {
        prefixed.StringIncrement("key", 123L, TimeSpan.FromSeconds(5), lowerBound: 10, upperBound: 200, flags: CommandFlags.None, options: IncrementOptions.None);
        mock.Verify(x => x.StringIncrement("prefix:key", 123L, TimeSpan.FromSeconds(5), 10, 200, IncrementOptions.None, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_increment_4()
    {
        prefixed.StringIncrement("key", 1.23, TimeSpan.FromSeconds(5), lowerBound: -1.0, upperBound: 2.0, flags: CommandFlags.None, options: IncrementOptions.Saturate);
        mock.Verify(x => x.StringIncrement("prefix:key", 1.23, TimeSpan.FromSeconds(5), -1.0, 2.0, IncrementOptions.Saturate, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_length()
    {
        prefixed.StringLength("key", CommandFlags.None);
        mock.Verify(x => x.StringLength("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_set_1()
    {
        TimeSpan expiry = TimeSpan.FromSeconds(123);
        prefixed.StringSet("key", "value", expiry, When.Exists, CommandFlags.None);
        mock.Verify(x => x.StringSet("prefix:key", "value", expiry, When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_set_2()
    {
        TimeSpan? expiry = null;
        prefixed.StringSet("key", "value", expiry, true, When.Exists, CommandFlags.None);
        mock.Verify(x => x.StringSet("prefix:key", "value", expiry, true, When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_set_3()
    {
        KeyValuePair<RedisKey, RedisValue>[] values = [new KeyValuePair<RedisKey, RedisValue>("a", "x"), new KeyValuePair<RedisKey, RedisValue>("b", "y")];
        Expression<Func<KeyValuePair<RedisKey, RedisValue>[], bool>> valid = _ => _.Length == 2 && _[0].Key == "prefix:a" && _[0].Value == "x" && _[1].Key == "prefix:b" && _[1].Value == "y";
        prefixed.StringSet(values, When.Exists, CommandFlags.None);
        mock.Verify(x => x.StringSet(It.Is(valid), When.Exists, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_set_compat()
    {
        TimeSpan? expiry = null;
        prefixed.StringSet("key", "value", expiry, When.Exists);
        mock.Verify(x => x.StringSet("prefix:key", "value", expiry, When.Exists), Times.AtLeastOnce());
    }

    [Fact]
    public void string_set_bit()
    {
        prefixed.StringSetBit("key", 123, true, CommandFlags.None);
        mock.Verify(x => x.StringSetBit("prefix:key", 123, true, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_set_range()
    {
        prefixed.StringSetRange("key", 123, "value", CommandFlags.None);
        mock.Verify(x => x.StringSetRange("prefix:key", 123, "value", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void execute_1()
    {
        prefixed.Execute("CUSTOM", "arg1", (RedisKey)"arg2");
        mock.Verify(x => x.Execute("CUSTOM", It.Is<object[]>(args => args.Length == 2 && args[0].Equals("arg1") && args[1].Equals((RedisKey)"prefix:arg2")), CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void execute_2()
    {
        var args = new List<object> { "arg1", (RedisKey)"arg2" };
        prefixed.Execute("CUSTOM", args, CommandFlags.None);
        mock.Verify(x => x.Execute("CUSTOM", It.Is<ICollection<object>>(a => a.Count == 2 && a.ElementAt(0).Equals("arg1") && a.ElementAt(1).Equals((RedisKey)"prefix:arg2"))!, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_add_1()
    {
        prefixed.GeoAdd("key", 1.23, 4.56, "member", CommandFlags.None);
        mock.Verify(x => x.GeoAdd("prefix:key", 1.23, 4.56, "member", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_add_2()
    {
        var geoEntry = new GeoEntry(1.23, 4.56, "member");
        prefixed.GeoAdd("key", geoEntry, CommandFlags.None);
        mock.Verify(x => x.GeoAdd("prefix:key", geoEntry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_add_3()
    {
        var geoEntries = new GeoEntry[] { new GeoEntry(1.23, 4.56, "member1") };
        prefixed.GeoAdd("key", geoEntries, CommandFlags.None);
        mock.Verify(x => x.GeoAdd("prefix:key", geoEntries, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_remove()
    {
        prefixed.GeoRemove("key", "member", CommandFlags.None);
        mock.Verify(x => x.GeoRemove("prefix:key", "member", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_distance()
    {
        prefixed.GeoDistance("key", "member1", "member2", GeoUnit.Meters, CommandFlags.None);
        mock.Verify(x => x.GeoDistance("prefix:key", "member1", "member2", GeoUnit.Meters, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_hash_1()
    {
        prefixed.GeoHash("key", "member", CommandFlags.None);
        mock.Verify(x => x.GeoHash("prefix:key", "member", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_hash_2()
    {
        var members = new RedisValue[] { "member1", "member2" };
        prefixed.GeoHash("key", members, CommandFlags.None);
        mock.Verify(x => x.GeoHash("prefix:key", members, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_position_1()
    {
        prefixed.GeoPosition("key", "member", CommandFlags.None);
        mock.Verify(x => x.GeoPosition("prefix:key", "member", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_position_2()
    {
        var members = new RedisValue[] { "member1", "member2" };
        prefixed.GeoPosition("key", members, CommandFlags.None);
        mock.Verify(x => x.GeoPosition("prefix:key", members, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_radius_1()
    {
        prefixed.GeoRadius("key", "member", 100, GeoUnit.Meters, 10, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None);
        mock.Verify(x => x.GeoRadius("prefix:key", "member", 100, GeoUnit.Meters, 10, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_radius_2()
    {
        prefixed.GeoRadius("key", 1.23, 4.56, 100, GeoUnit.Meters, 10, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None);
        mock.Verify(x => x.GeoRadius("prefix:key", 1.23, 4.56, 100, GeoUnit.Meters, 10, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_search_1()
    {
        var shape = new GeoSearchCircle(100, GeoUnit.Meters);
        prefixed.GeoSearch("key", "member", shape, 10, true, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None);
        mock.Verify(x => x.GeoSearch("prefix:key", "member", shape, 10, true, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_search_2()
    {
        var shape = new GeoSearchCircle(100, GeoUnit.Meters);
        prefixed.GeoSearch("key", 1.23, 4.56, shape, 10, true, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None);
        mock.Verify(x => x.GeoSearch("prefix:key", 1.23, 4.56, shape, 10, true, Order.Ascending, GeoRadiusOptions.Default, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_search_and_store_1()
    {
        var shape = new GeoSearchCircle(100, GeoUnit.Meters);
        prefixed.GeoSearchAndStore("source", "destination", "member", shape, 10, true, Order.Ascending, false, CommandFlags.None);
        mock.Verify(x => x.GeoSearchAndStore("prefix:source", "prefix:destination", "member", shape, 10, true, Order.Ascending, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void geo_search_and_store_2()
    {
        var shape = new GeoSearchCircle(100, GeoUnit.Meters);
        prefixed.GeoSearchAndStore("source", "destination", 1.23, 4.56, shape, 10, true, Order.Ascending, false, CommandFlags.None);
        mock.Verify(x => x.GeoSearchAndStore("prefix:source", "prefix:destination", 1.23, 4.56, shape, 10, true, Order.Ascending, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_field_expire_1()
    {
        var hashFields = new RedisValue[] { "field1", "field2" };
        var expiry = TimeSpan.FromSeconds(60);
        prefixed.HashFieldExpire("key", hashFields, expiry, ExpireWhen.Always, CommandFlags.None);
        mock.Verify(x => x.HashFieldExpire("prefix:key", hashFields, expiry, ExpireWhen.Always, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_field_expire_2()
    {
        var hashFields = new RedisValue[] { "field1", "field2" };
        var expiry = DateTime.Now.AddMinutes(1);
        prefixed.HashFieldExpire("key", hashFields, expiry, ExpireWhen.Always, CommandFlags.None);
        mock.Verify(x => x.HashFieldExpire("prefix:key", hashFields, expiry, ExpireWhen.Always, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_field_get_expire_date_time()
    {
        var hashFields = new RedisValue[] { "field1", "field2" };
        prefixed.HashFieldGetExpireDateTime("key", hashFields, CommandFlags.None);
        mock.Verify(x => x.HashFieldGetExpireDateTime("prefix:key", hashFields, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_field_persist()
    {
        var hashFields = new RedisValue[] { "field1", "field2" };
        prefixed.HashFieldPersist("key", hashFields, CommandFlags.None);
        mock.Verify(x => x.HashFieldPersist("prefix:key", hashFields, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_field_get_time_to_live()
    {
        var hashFields = new RedisValue[] { "field1", "field2" };
        prefixed.HashFieldGetTimeToLive("key", hashFields, CommandFlags.None);
        mock.Verify(x => x.HashFieldGetTimeToLive("prefix:key", hashFields, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_get_lease()
    {
        prefixed.HashGetLease("key", "field", CommandFlags.None);
        mock.Verify(x => x.HashGetLease("prefix:key", "field", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_field_get_and_delete_1()
    {
        prefixed.HashFieldGetAndDelete("key", "field", CommandFlags.None);
        mock.Verify(x => x.HashFieldGetAndDelete("prefix:key", "field", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_field_get_and_delete_2()
    {
        var hashFields = new RedisValue[] { "field1", "field2" };
        prefixed.HashFieldGetAndDelete("key", hashFields, CommandFlags.None);
        mock.Verify(x => x.HashFieldGetAndDelete("prefix:key", hashFields, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_field_get_lease_and_delete()
    {
        prefixed.HashFieldGetLeaseAndDelete("key", "field", CommandFlags.None);
        mock.Verify(x => x.HashFieldGetLeaseAndDelete("prefix:key", "field", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_field_get_and_set_expiry_1()
    {
        var expiry = TimeSpan.FromMinutes(5);
        prefixed.HashFieldGetAndSetExpiry("key", "field", expiry, false, CommandFlags.None);
        mock.Verify(x => x.HashFieldGetAndSetExpiry("prefix:key", "field", expiry, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_field_get_and_set_expiry_2()
    {
        var expiry = DateTime.Now.AddMinutes(5);
        prefixed.HashFieldGetAndSetExpiry("key", "field", expiry, CommandFlags.None);
        mock.Verify(x => x.HashFieldGetAndSetExpiry("prefix:key", "field", expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_field_get_lease_and_set_expiry_1()
    {
        var expiry = TimeSpan.FromMinutes(5);
        prefixed.HashFieldGetLeaseAndSetExpiry("key", "field", expiry, false, CommandFlags.None);
        mock.Verify(x => x.HashFieldGetLeaseAndSetExpiry("prefix:key", "field", expiry, false, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void hash_field_get_lease_and_set_expiry_2()
    {
        var expiry = DateTime.Now.AddMinutes(5);
        prefixed.HashFieldGetLeaseAndSetExpiry("key", "field", expiry, CommandFlags.None);
        mock.Verify(x => x.HashFieldGetLeaseAndSetExpiry("prefix:key", "field", expiry, CommandFlags.None), Times.AtLeastOnce());
    }
    [Fact]
    public void string_get_lease()
    {
        prefixed.StringGetLease("key", CommandFlags.None);
        mock.Verify(x => x.StringGetLease("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_get_set_expiry_1()
    {
        var expiry = TimeSpan.FromMinutes(5);
        prefixed.StringGetSetExpiry("key", expiry, CommandFlags.None);
        mock.Verify(x => x.StringGetSetExpiry("prefix:key", expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_get_set_expiry_2()
    {
        var expiry = DateTime.Now.AddMinutes(5);
        prefixed.StringGetSetExpiry("key", expiry, CommandFlags.None);
        mock.Verify(x => x.StringGetSetExpiry("prefix:key", expiry, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_set_and_get_1()
    {
        var expiry = TimeSpan.FromMinutes(5);
        prefixed.StringSetAndGet("key", "value", expiry, When.Always, CommandFlags.None);
        mock.Verify(x => x.StringSetAndGet("prefix:key", "value", expiry, When.Always, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_set_and_get_2()
    {
        var expiry = TimeSpan.FromMinutes(5);
        prefixed.StringSetAndGet("key", "value", expiry, false, When.Always, CommandFlags.None);
        mock.Verify(x => x.StringSetAndGet("prefix:key", "value", expiry, false, When.Always, CommandFlags.None), Times.AtLeastOnce());
    }
    [Fact]
    public void string_longest_common_subsequence()
    {
        prefixed.StringLongestCommonSubsequence("key1", "key2", CommandFlags.None);
        mock.Verify(x => x.StringLongestCommonSubsequence("prefix:key1", "prefix:key2", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_longest_common_subsequence_length()
    {
        prefixed.StringLongestCommonSubsequenceLength("key1", "key2", CommandFlags.None);
        mock.Verify(x => x.StringLongestCommonSubsequenceLength("prefix:key1", "prefix:key2", CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void string_longest_common_subsequence_with_matches()
    {
        prefixed.StringLongestCommonSubsequenceWithMatches("key1", "key2", 5, CommandFlags.None);
        mock.Verify(x => x.StringLongestCommonSubsequenceWithMatches("prefix:key1", "prefix:key2", 5, CommandFlags.None), Times.AtLeastOnce());
    }
    [Fact]
    public void is_connected()
    {
        prefixed.IsConnected("key", CommandFlags.None);
        mock.Verify(x => x.IsConnected("prefix:key", CommandFlags.None), Times.AtLeastOnce());
    }
    [Fact]
    public void stream_add_with_trim_mode_1()
    {
        prefixed.StreamAdd("key", "field", "value", "*", 1000, false, 100, StreamTrimMode.KeepReferences, CommandFlags.None);
        mock.Verify(x => x.StreamAdd("prefix:key", "field", "value", "*", 1000, false, 100, StreamTrimMode.KeepReferences, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_add_with_trim_mode_2()
    {
        var fields = new NameValueEntry[] { new NameValueEntry("field", "value") };
        prefixed.StreamAdd("key", fields, "*", 1000, false, 100, StreamTrimMode.KeepReferences, CommandFlags.None);
        mock.Verify(x => x.StreamAdd("prefix:key", fields, "*", 1000, false, 100, StreamTrimMode.KeepReferences, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_add_with_options_1()
    {
        var options = new StreamAddOptions { MaxLength = 1000, CreateStream = false };
        prefixed.StreamAdd("key", "field", "value", options, CommandFlags.None);
        mock.Verify(x => x.StreamAdd("prefix:key", "field", "value", options, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_add_with_options_2()
    {
        var fields = new NameValueEntry[] { new NameValueEntry("field", "value") };
        var options = new StreamAddOptions { MinId = "5-5", CreateStream = false };
        prefixed.StreamAdd("key", fields, options, CommandFlags.None);
        mock.Verify(x => x.StreamAdd("prefix:key", fields, options, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_trim_with_mode()
    {
        prefixed.StreamTrim("key", 1000, false, 100, StreamTrimMode.KeepReferences, CommandFlags.None);
        mock.Verify(x => x.StreamTrim("prefix:key", 1000, false, 100, StreamTrimMode.KeepReferences, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_trim_by_min_id_with_mode()
    {
        prefixed.StreamTrimByMinId("key", "1111111111", false, 100, StreamTrimMode.KeepReferences, CommandFlags.None);
        mock.Verify(x => x.StreamTrimByMinId("prefix:key", "1111111111", false, 100, StreamTrimMode.KeepReferences, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_read_group_with_no_ack_1()
    {
        prefixed.StreamReadGroup("key", "group", "consumer", "0-0", 10, true, CommandFlags.None);
        mock.Verify(x => x.StreamReadGroup("prefix:key", "group", "consumer", "0-0", 10, true, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_read_group_with_no_ack_2()
    {
        var streamPositions = new StreamPosition[] { new StreamPosition("key", "0-0") };
        prefixed.StreamReadGroup(streamPositions, "group", "consumer", 10, true, CommandFlags.None);
        mock.Verify(x => x.StreamReadGroup(streamPositions, "group", "consumer", 10, true, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_trim_simple()
    {
        prefixed.StreamTrim("key", 1000, true, CommandFlags.None);
        mock.Verify(x => x.StreamTrim("prefix:key", 1000, true, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_read_group_simple_1()
    {
        prefixed.StreamReadGroup("key", "group", "consumer", "0-0", 10, CommandFlags.None);
        mock.Verify(x => x.StreamReadGroup("prefix:key", "group", "consumer", "0-0", 10, CommandFlags.None), Times.AtLeastOnce());
    }

    [Fact]
    public void stream_read_group_simple_2()
    {
        var streamPositions = new StreamPosition[] { new StreamPosition("key", "0-0") };
        prefixed.StreamReadGroup(streamPositions, "group", "consumer", 10, CommandFlags.None);
        mock.Verify(x => x.StreamReadGroup(streamPositions, "group", "consumer", 10, CommandFlags.None), Times.AtLeastOnce());
    }
}

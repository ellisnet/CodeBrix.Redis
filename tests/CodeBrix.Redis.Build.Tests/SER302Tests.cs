using System.Threading.Tasks;
using Xunit;

namespace CodeBrix.Redis.Build.Tests; //was previously: StackExchange.Redis.Build.Tests;

/// <summary>
/// Family C: the condition checks what the queued command already reports, so the transaction buys nothing.
/// </summary>
public class SER302Tests : Verifier<TransactionAnalyzer>
{
    [Fact]
    public Task set_not_contains_guarding_set_add_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                {|#0:tran.AddCondition(Condition.SetNotContains(key, "member"))|};
                _ = tran.SetAddAsync(key, "member");
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER302").WithLocation(0).WithArguments(
            "Condition.SetNotContains",
            "SetAddAsync",
            "SetAdd[Async](key, value), which returns false if the member was already there"));

    [Fact]
    public Task set_contains_guarding_set_remove_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                {|#0:tran.AddCondition(Condition.SetContains(key, "member"))|};
                _ = tran.SetRemoveAsync(key, "member");
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER302").WithLocation(0).WithArguments(
            "Condition.SetContains",
            "SetRemoveAsync",
            "SetRemove[Async](key, value), which returns false if the member was not there"));

    [Fact]
    public Task sorted_set_contains_guarding_sorted_set_remove_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                {|#0:tran.AddCondition(Condition.SortedSetContains(key, "member"))|};
                _ = tran.SortedSetRemoveAsync(key, "member");
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER302").WithLocation(0).WithArguments(
            "Condition.SortedSetContains",
            "SortedSetRemoveAsync",
            "SortedSetRemove[Async](key, member), which returns false if the member was not there"));

    [Fact]
    public Task hash_exists_guarding_hash_delete_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                {|#0:tran.AddCondition(Condition.HashExists(key, "field"))|};
                _ = tran.HashDeleteAsync(key, "field");
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER302").WithLocation(0).WithArguments(
            "Condition.HashExists",
            "HashDeleteAsync",
            "HashDelete[Async](key, field), which returns false if the field was not there"));

    [Fact]
    public Task key_exists_guarding_key_delete_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                {|#0:tran.AddCondition(Condition.KeyExists(key))|};
                _ = tran.KeyDeleteAsync(key);
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER302").WithLocation(0).WithArguments(
            "Condition.KeyExists",
            "KeyDeleteAsync",
            "KeyDelete[Async](key), which returns false if the key did not exist"));

    [Fact]
    public Task key_exists_guarding_key_expire_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                {|#0:tran.AddCondition(Condition.KeyExists(key))|};
                _ = tran.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER302").WithLocation(0).WithArguments(
            "Condition.KeyExists",
            "KeyExpireAsync",
            "KeyExpire[Async](key, expiry), which returns false if the key did not exist"));

    [Fact]
    // LSET reports an out-of-range index by throwing, not by returning false - ListSetByIndex returns Task,
    // not Task<bool> - so dropping the condition would turn an aborted transaction into an exception. That is
    // a behaviour change, not a simplification, so this stays quiet.
    public Task list_index_exists_guarding_list_set_by_index_is_not_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                tran.AddCondition(Condition.ListIndexExists(key, 0));
                _ = tran.ListSetByIndexAsync(key, 0, "value");
                await tran.ExecuteAsync();
            }
        }
        """);

    [Fact]
    // Same key, different member: the condition asks about "a" and the command removes "b", so it is a real
    // guard and dropping it would change behaviour. The key matching is not enough on its own.
    public Task different_member_is_not_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                tran.AddCondition(Condition.SetContains(key, "a"));
                _ = tran.SetRemoveAsync(key, "b");
                await tran.ExecuteAsync();
            }
        }
        """);

    [Fact]
    // and the same for a hash field
    public Task different_hash_field_is_not_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                tran.AddCondition(Condition.HashExists(key, "a"));
                _ = tran.HashDeleteAsync(key, "b");
                await tran.ExecuteAsync();
            }
        }
        """);
}

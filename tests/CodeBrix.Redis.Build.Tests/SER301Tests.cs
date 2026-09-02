using System.Threading.Tasks;
using Xunit;

namespace CodeBrix.Redis.Build.Tests; //was previously: StackExchange.Redis.Build.Tests;

/// <summary>
/// Family B: compare-and-set, where a newer single command subsumes both the condition and the write. Separate
/// from SER300 because these need an 8.4+ server and SER300 does not.
/// </summary>
public class SER301Tests : Verifier<TransactionAnalyzer>
{
    [Fact]
    public Task string_equal_guarding_string_set_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                {|#0:tran.AddCondition(Condition.StringEqual(key, "old"))|};
                _ = tran.StringSetAsync(key, "new");
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER301").WithLocation(0).WithArguments(
            "Condition.StringEqual",
            "StringSetAsync",
            "StringSet[Async](key, value, ValueCondition.Equal(expected))",
            "8.4"));

    [Fact]
    public Task string_not_equal_guarding_string_set_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                {|#0:tran.AddCondition(Condition.StringNotEqual(key, "old"))|};
                _ = tran.StringSetAsync(key, "new");
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER301").WithLocation(0).WithArguments(
            "Condition.StringNotEqual",
            "StringSetAsync",
            "StringSet[Async](key, value, ValueCondition.NotEqual(expected))",
            "8.4"));

    [Fact]
    // the canonical lock-release, and the highest-frequency real-world hit in this family
    public Task string_equal_guarding_key_delete_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                {|#0:tran.AddCondition(Condition.StringEqual(key, "token"))|};
                _ = tran.KeyDeleteAsync(key);
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER301").WithLocation(0).WithArguments(
            "Condition.StringEqual",
            "KeyDeleteAsync",
            "StringDelete[Async](key, ValueCondition.Equal(expected)), or LockRelease[Async]",
            "8.4"));

    [Fact]
    public Task string_not_equal_guarding_key_delete_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                {|#0:tran.AddCondition(Condition.StringNotEqual(key, "token"))|};
                _ = tran.KeyDeleteAsync(key);
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER301").WithLocation(0).WithArguments(
            "Condition.StringNotEqual",
            "KeyDeleteAsync",
            "StringDelete[Async](key, ValueCondition.NotEqual(expected))",
            "8.4"));

    [Fact]
    // cross-key compare-and-set genuinely needs the transaction; must never fire
    public Task different_keys_is_not_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey a, RedisKey b)
            {
                var tran = db.CreateTransaction();
                tran.AddCondition(Condition.StringEqual(a, "old"));
                _ = tran.StringSetAsync(b, "new");
                await tran.ExecuteAsync();
            }
        }
        """);
}

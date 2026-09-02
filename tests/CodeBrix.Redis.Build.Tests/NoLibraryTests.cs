using System.Threading.Tasks;
using Xunit;

namespace CodeBrix.Redis.Build.Tests; //was previously: StackExchange.Redis.Build.Tests;

/// <summary>
/// The analyzer ships to every consumer of the package, including projects that reference it only
/// transitively and never touch a transaction. Those compilations must get nothing at all.
/// </summary>
public class NoLibraryTests : Verifier<TransactionAnalyzer>
{
    [Fact]
    // The decoy is the point: identical member names, identical shape, different symbols. If the analyzer ever
    // matched on names instead of resolved types, this would fire - and would fire on unrelated user code.
    public Task lookalike_api_in_another_namespace_is_not_flagged() => VerifyWithoutLibraryAsync(
        """
        using System.Threading.Tasks;
        namespace NotRedis
        {
            public static class Condition
            {
                public static object StringEqual(string key, string value) => new object();
                public static object KeyNotExists(string key) => new object();
            }

            public interface ITransaction
            {
                void AddCondition(object condition);
                Task<bool> StringSetAsync(string key, string value);
                Task<bool> ExecuteAsync();
            }

            class C
            {
                public async Task M(ITransaction tran, string key)
                {
                    tran.AddCondition(Condition.StringEqual(key, "old"));
                    _ = tran.StringSetAsync(key, "new");
                    await tran.ExecuteAsync();
                }
            }
        }
        """);

    [Fact]
    // the ordinary case for nearly every compilation on earth: no such library, nothing to say
    public Task unrelated_code_is_not_flagged() => VerifyWithoutLibraryAsync(
        """
        class C
        {
            public int M(int x) => x + 1;
        }
        """);
}

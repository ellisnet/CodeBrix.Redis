using System.Threading.Tasks;
using Xunit;

namespace CodeBrix.Redis.Build.Tests; //was previously: StackExchange.Redis.Build.Tests;

/// <summary>
/// Version gating: an analyzer cannot see the server, so a project can declare its floor and get only the
/// suggestions it can act on.
/// </summary>
public class MinServerVersionTests : Verifier<TransactionAnalyzer>
{
    private const string CompareAndSet =
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
        """;

    private const string ConditionalArgument =
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                {|#0:tran.AddCondition(Condition.KeyNotExists(key))|};
                _ = tran.StringSetAsync(key, "value");
                await tran.ExecuteAsync();
            }
        }
        """;

    [Fact]
    // the default: nobody has said anything about servers, so show the suggestion. Silence by default would
    // hide the rule from exactly the people who have not thought about this yet
    public Task unset_shows_version_gated_suggestion() => VerifyAsync(
        CompareAndSet,
        Diagnostic("SER301").WithLocation(0));

    [Fact]
    public Task newer_than_required_shows_suggestion() => VerifyWithMinServerVersionAsync(
        CompareAndSet,
        "8.6",
        Diagnostic("SER301").WithLocation(0));

    [Fact]
    // exactly the required version counts as supported
    public Task exactly_required_shows_suggestion() => VerifyWithMinServerVersionAsync(
        CompareAndSet,
        "8.4",
        Diagnostic("SER301").WithLocation(0));

    [Fact]
    // the point of the whole exercise: compare-and-set needs 8.4, so do not suggest it to someone on 7.4
    public Task older_than_required_hides_suggestion() => VerifyWithMinServerVersionAsync(
        CompareAndSet,
        "7.4");

    [Fact]
    // ... but the version-free family must survive the same setting, which is why they have separate IDs
    public Task older_than_required_still_shows_version_free_suggestion() => VerifyWithMinServerVersionAsync(
        ConditionalArgument,
        "2.8",
        Diagnostic("SER300").WithLocation(0));

    [Fact]
    // a major-only value is a reasonable thing to write
    public Task major_only_is_understood() => VerifyWithMinServerVersionAsync(
        CompareAndSet,
        "7");

    [Fact]
    // a patch component is accepted and ignored rather than rejected
    public Task patch_component_is_ignored() => VerifyWithMinServerVersionAsync(
        CompareAndSet,
        "8.4.1",
        Diagnostic("SER301").WithLocation(0));

    [Fact]
    // an unreadable value falls back to showing everything: silently hiding suggestions over a typo would be
    // near-impossible to diagnose from the outside
    public Task unparseable_shows_everything() => VerifyWithMinServerVersionAsync(
        CompareAndSet,
        "not-a-version",
        Diagnostic("SER301").WithLocation(0));

    [Fact]
    // the version reaches the message, so the reader knows what "newer" means without following the link
    public Task message_names_the_required_version() => VerifyAsync(
        CompareAndSet,
        Diagnostic("SER301").WithLocation(0).WithArguments(
            "Condition.StringEqual",
            "StringSetAsync",
            "StringSet[Async](key, value, ValueCondition.Equal(expected))",
            "8.4"));
}

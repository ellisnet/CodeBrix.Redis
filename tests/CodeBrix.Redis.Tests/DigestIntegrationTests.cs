using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class DigestIntegrationTests(ITestOutputHelper output, SharedConnectionFixture fixture)
    : TestBase(output, fixture)
{
    [Fact]
    public async Task read_digest()
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        byte[] blob = new byte[1024];
        new Random().NextBytes(blob);
        var local = ValueCondition.CalculateDigest(blob);
        local.Kind.Should().Be(ValueCondition.ConditionKind.DigestEquals);
        local.Value.Type.Should().Be(RedisValue.StorageType.Int64);
        Log("Local digest: " + local);

        var key = Me();
        var db = conn.GetDatabase();
        await db.KeyDeleteAsync(key, flags: CommandFlags.FireAndForget);

        // test without a value
        var digest = await db.StringDigestAsync(key);
        digest.Should().BeNull();

        // test with a value
        await db.StringSetAsync(key, blob, flags: CommandFlags.FireAndForget);
        digest = await db.StringDigestAsync(key);
        Assert.NotNull(digest);
        digest.Value.Kind.Should().Be(ValueCondition.ConditionKind.DigestEquals);
        digest.Value.Value.Type.Should().Be(RedisValue.StorageType.Int64);
        Log("Server digest: " + digest);
        digest.Value.Should().Be(local);
    }

    [Theory]
    [InlineData(null, (int)ValueCondition.ConditionKind.NotExists)]
    [InlineData("new value", (int)ValueCondition.ConditionKind.NotExists)]
    [InlineData(null, (int)ValueCondition.ConditionKind.ValueEquals)]
    [InlineData(null, (int)ValueCondition.ConditionKind.DigestEquals)]
    public async Task invalid_conditional_delete(string? testValue, int rawKind)
    {
        await using var conn = Create(); // no server requirement, since fails locally
        var key = Me();
        var db = conn.GetDatabase();
        var condition = CreateCondition(testValue, rawKind);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await db.StringDeleteAsync(key, when: condition);
        });
        ex.Message.Should().StartWith("StringDeleteAsync cannot be used with a NotExists condition.");
    }

    [Theory]
    [InlineData(null, null, (int)ValueCondition.ConditionKind.Always)]
    [InlineData(null, "new value", (int)ValueCondition.ConditionKind.Always)]
    [InlineData("old value", "new value", (int)ValueCondition.ConditionKind.Always, true)]
    [InlineData("new value", "new value", (int)ValueCondition.ConditionKind.Always, true)]

    [InlineData(null, null, (int)ValueCondition.ConditionKind.Exists)]
    [InlineData(null, "new value", (int)ValueCondition.ConditionKind.Exists)]
    [InlineData("old value", "new value", (int)ValueCondition.ConditionKind.Exists, true)]
    [InlineData("new value", "new value", (int)ValueCondition.ConditionKind.Exists, true)]

    [InlineData(null, "new value", (int)ValueCondition.ConditionKind.DigestEquals)]
    [InlineData("old value", "new value", (int)ValueCondition.ConditionKind.DigestEquals)]
    [InlineData("new value", "new value", (int)ValueCondition.ConditionKind.DigestEquals, true)]

    [InlineData(null, "new value", (int)ValueCondition.ConditionKind.ValueEquals)]
    [InlineData("old value", "new value", (int)ValueCondition.ConditionKind.ValueEquals)]
    [InlineData("new value", "new value", (int)ValueCondition.ConditionKind.ValueEquals, true)]

    [InlineData(null, null, (int)ValueCondition.ConditionKind.DigestNotEquals)]
    [InlineData(null, "new value", (int)ValueCondition.ConditionKind.DigestNotEquals)]
    [InlineData("old value", "new value", (int)ValueCondition.ConditionKind.DigestNotEquals, true)]
    [InlineData("new value", "new value", (int)ValueCondition.ConditionKind.DigestNotEquals)]

    [InlineData(null, null, (int)ValueCondition.ConditionKind.ValueNotEquals)]
    [InlineData(null, "new value", (int)ValueCondition.ConditionKind.ValueNotEquals)]
    [InlineData("old value", "new value", (int)ValueCondition.ConditionKind.ValueNotEquals, true)]
    [InlineData("new value", "new value", (int)ValueCondition.ConditionKind.ValueNotEquals)]
    public async Task conditional_delete(string? dbValue, string? testValue, int rawKind, bool expectDelete = false)
    {
        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var key = Me();
        var db = conn.GetDatabase();
        await db.KeyDeleteAsync(key, flags: CommandFlags.FireAndForget);
        if (dbValue != null) await db.StringSetAsync(key, dbValue, flags: CommandFlags.FireAndForget);

        var condition = CreateCondition(testValue, rawKind);

        var pendingDelete = db.StringDeleteAsync(key, when: condition);
        var exists = await db.KeyExistsAsync(key);
        var deleted = await pendingDelete;

        if (dbValue is null)
        {
            // didn't exist to be deleted
            expectDelete.Should().BeFalse();
            exists.Should().BeFalse();
            deleted.Should().BeFalse();
        }
        else
        {
            deleted.Should().Be(expectDelete);
            exists.Should().Be(!expectDelete);
        }
    }

    private ValueCondition CreateCondition(string? testValue, int rawKind)
    {
        var condition = (ValueCondition.ConditionKind)rawKind switch
        {
            ValueCondition.ConditionKind.Always => ValueCondition.Always,
            ValueCondition.ConditionKind.Exists => ValueCondition.Exists,
            ValueCondition.ConditionKind.NotExists => ValueCondition.NotExists,
            ValueCondition.ConditionKind.ValueEquals => ValueCondition.Equal(testValue),
            ValueCondition.ConditionKind.ValueNotEquals => ValueCondition.NotEqual(testValue),
            ValueCondition.ConditionKind.DigestEquals => ValueCondition.DigestEqual(testValue),
            ValueCondition.ConditionKind.DigestNotEquals => ValueCondition.DigestNotEqual(testValue),
            _ => throw new ArgumentOutOfRangeException(nameof(rawKind)),
        };
        Log($"Condition: {condition}");
        return condition;
    }

    [Fact]
    public async Task leading_zero_formatting()
    {
        // Example generated that hashes to 0x00006c38adf31777; see https://github.com/redis/redis/issues/14496
        var localDigest =
            ValueCondition.CalculateDigest("v8lf0c11xh8ymlqztfd3eeq16kfn4sspw7fqmnuuq3k3t75em5wdizgcdw7uc26nnf961u2jkfzkjytls2kwlj7626sd"u8);
        Log($"local: {localDigest}");
        localDigest.ToString().Should().Be("IFDEQ 00006c38adf31777");

        await using var conn = Create(require: RedisFeatures.v8_4_0_rc1);
        var key = Me();
        var db = conn.GetDatabase();
        await db.KeyDeleteAsync(key, flags: CommandFlags.FireAndForget);
        await db.StringSetAsync(key, "v8lf0c11xh8ymlqztfd3eeq16kfn4sspw7fqmnuuq3k3t75em5wdizgcdw7uc26nnf961u2jkfzkjytls2kwlj7626sd", flags: CommandFlags.FireAndForget);
        var pendingDigest = db.StringDigestAsync(key);
        var pendingDeleted = db.StringDeleteAsync(key, when: localDigest);
        var existsAfter = await db.KeyExistsAsync(key);

        var serverDigest = await pendingDigest;
        Log($"server: {serverDigest}");
        serverDigest.Should().Be(localDigest);
        (await pendingDeleted).Should().BeTrue();
        existsAfter.Should().BeFalse();
    }
}

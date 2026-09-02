using System;
using System.Threading.Tasks;
using CodeBrix.Redis.KeyspaceIsolation;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class WithKeyPrefixTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task blank_prefix_yields_same_bytes()
    {
        //Arrange
        await using var conn = Create();

        var raw = conn.GetDatabase();

        //Act
        var prefixed = raw.WithKeyPrefix(Array.Empty<byte>());

        //Assert
        prefixed.Should().BeSameAs(raw);
    }

    [Fact]
    public async Task blank_prefix_yields_same_string()
    {
        //Arrange
        await using var conn = Create();

        var raw = conn.GetDatabase();

        //Act
        var prefixed = raw.WithKeyPrefix("");

        //Assert
        prefixed.Should().BeSameAs(raw);
    }

    [Fact]
    public async Task null_prefix_is_error_bytes()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await using var conn = Create();

            var raw = conn.GetDatabase();
            raw.WithKeyPrefix((byte[]?)null);
        });
    }

    [Fact]
    public async Task null_prefix_is_error_string()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await using var conn = Create();

            var raw = conn.GetDatabase();
            raw.WithKeyPrefix((string?)null);
        });
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void null_database_is_error(string? prefix)
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            IDatabase? raw = null;
            raw!.WithKeyPrefix(prefix);
        });
    }

    [Fact]
    public async Task basic_smoke_test()
    {
        await using var conn = Create();

        var raw = conn.GetDatabase();

        var prefix = Me();
        var foo = raw.WithKeyPrefix(prefix);
        var foobar = foo.WithKeyPrefix("bar");

        string key = Me();

        string s = Guid.NewGuid().ToString(), t = Guid.NewGuid().ToString();

        foo.StringSet(key, s, flags: CommandFlags.FireAndForget);
        var val = (string?)foo.StringGet(key);
        val.Should().Be(s); // fooBasicSmokeTest

        foobar.StringSet(key, t, flags: CommandFlags.FireAndForget);
        val = foobar.StringGet(key);
        val.Should().Be(t); // foobarBasicSmokeTest

        val = foo.StringGet("bar" + key);
        val.Should().Be(t); // foobarBasicSmokeTest

        val = raw.StringGet(prefix + key);
        val.Should().Be(s); // fooBasicSmokeTest

        val = raw.StringGet(prefix + "bar" + key);
        val.Should().Be(t); // foobarBasicSmokeTest
    }

    [Fact]
    public async Task condition_test()
    {
        await using var conn = Create();

        var raw = conn.GetDatabase();

        var prefix = Me() + ":";
        var foo = raw.WithKeyPrefix(prefix);
        Output.WriteLine($"prefixed db features: {foo}"); // should be KeyPrefix (not Transaction/Batch)

        raw.KeyDelete(prefix + "abc", CommandFlags.FireAndForget);
        raw.KeyDelete(prefix + "i", CommandFlags.FireAndForget);

        // execute while key exists
        raw.StringSet(prefix + "abc", "def", flags: CommandFlags.FireAndForget);
        var tran = foo.CreateTransaction();
        tran.AddCondition(Condition.KeyExists("abc"));
        _ = tran.StringIncrementAsync("i");
        tran.Execute();

        int i = (int)raw.StringGet(prefix + "i");
        i.Should().Be(1);

        // repeat without key
        raw.KeyDelete(prefix + "abc", CommandFlags.FireAndForget);
        tran = foo.CreateTransaction();
        tran.AddCondition(Condition.KeyExists("abc"));
        _ = tran.StringIncrementAsync("i");
        tran.Execute();

        i = (int)raw.StringGet(prefix + "i");
        i.Should().Be(1);
    }
}

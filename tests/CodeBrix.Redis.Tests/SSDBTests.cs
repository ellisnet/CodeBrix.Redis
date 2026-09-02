using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class SSDBTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task connect_to_ssdb()
    {
        Skip.IfNoConfig(nameof(TestConfig.Config.SSDBServer), TestConfig.Current.SSDBServer);

        await using var conn = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
        {
            EndPoints = { { TestConfig.Current.SSDBServer, TestConfig.Current.SSDBPort } },
            CommandMap = CommandMap.SSDB,
        });

        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringGet(key).IsNull.Should().BeTrue();
        db.StringSet(key, "abc", flags: CommandFlags.FireAndForget);
        db.StringGet(key).Should().Be("abc");
    }
}

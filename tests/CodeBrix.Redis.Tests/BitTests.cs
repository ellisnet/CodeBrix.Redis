using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class BitTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task basic_ops()
    {
        //Arrange
        await using var conn = Create();
        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        //Act
        db.StringSetBit(key, 10, true);

        //Assert
        db.StringGetBit(key, 10).Should().BeTrue();
        db.StringGetBit(key, 11).Should().BeFalse();
    }
}

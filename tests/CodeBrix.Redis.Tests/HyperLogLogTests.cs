using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class HyperLogLogTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task single_key_length()
    {
        //Arrange
        await using var conn = Create();
        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.HyperLogLogAdd(key, "a");
        db.HyperLogLogAdd(key, "b");

        //Act
        db.HyperLogLogAdd(key, "c");

        //Assert
        (db.HyperLogLogLength(key) > 0).Should().BeTrue();
    }

    [Fact]
    public async Task multi_key_length()
    {
        //Arrange
        await using var conn = Create();
        var db = conn.GetDatabase();
        var prefix = Me();
        RedisKey[] keys = [prefix + ":hll1", prefix + ":hll2", prefix + ":hll3"];
        db.HyperLogLogAdd(keys[0], "a");
        db.HyperLogLogAdd(keys[1], "b");

        //Act
        db.HyperLogLogAdd(keys[2], "c");

        //Assert
        (db.HyperLogLogLength(keys) > 0).Should().BeTrue();
    }
}

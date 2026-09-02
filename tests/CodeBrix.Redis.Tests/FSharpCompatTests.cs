using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class FSharpCompatTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public void redis_key_constructor()
    {
        (new RedisKey()).Should().Be(default(RedisKey));
        (new RedisKey("MyKey")).Should().Be((RedisKey)"MyKey");
        (new RedisKey(null, "MyKey2")).Should().Be((RedisKey)"MyKey2");
    }

    [Fact]
    public void redis_value_constructor()
    {
        (new RedisValue()).Should().Be(default(RedisValue));
        (new RedisValue("MyKey")).Should().Be((RedisValue)"MyKey");
        (new RedisValue("MyKey2")).Should().Be((RedisValue)"MyKey2");
    }
}

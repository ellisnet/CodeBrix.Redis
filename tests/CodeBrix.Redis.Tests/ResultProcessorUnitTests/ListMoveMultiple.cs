using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class ListMoveMultiple(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void moved_elements_success()
    {
        // LMOVEM src dst LEFT RIGHT COUNT 2 BULK => ["a", "b"]
        var resp = "*2\r\n$1\r\na\r\n$1\r\nb\r\n";

        var result = Execute(resp, ResultProcessor.NullableRedisValueArray);

        result.Should().NotBeNull();
        Join(result).Should().Be("a,b");
    }

    [Fact]
    public void empty_array_is_empty_not_null()
    {
        // an empty array must stay an empty array, distinct from a null reply.
        var resp = "*0\r\n";

        var result = Execute(resp, ResultProcessor.NullableRedisValueArray);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void null_array_resp2_is_null()
    {
        // EXACTLY not satisfied: RESP2 null array.
        var resp = "*-1\r\n";

        var result = Execute(resp, ResultProcessor.NullableRedisValueArray);

        result.Should().BeNull();
    }

    [Fact]
    public void null_resp3_is_null()
    {
        // EXACTLY not satisfied: RESP3 null.
        var resp = "_\r\n";

        var result = Execute(resp, ResultProcessor.NullableRedisValueArray, protocol: RedisProtocol.Resp3);

        result.Should().BeNull();
    }

    [Fact]
    public void scalar_failure()
    {
        // A bulk-string / scalar reply is not a valid LMOVEM response.
        var resp = "$5\r\nhello\r\n";

        ExecuteUnexpected(resp, ResultProcessor.NullableRedisValueArray);
    }

    [Fact]
    public void integer_failure()
    {
        var resp = ":5\r\n";

        ExecuteUnexpected(resp, ResultProcessor.NullableRedisValueArray);
    }
}

using System;
using System.IO;
using System.Text;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ValueTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public void null_value_checks()
    {
        RedisValue four = 4;
        four.IsNull.Should().BeFalse();
        four.IsInteger.Should().BeTrue();
        four.HasValue.Should().BeTrue();
        four.IsNullOrEmpty.Should().BeFalse();

        RedisValue n = default;
        n.IsNull.Should().BeTrue();
        n.IsInteger.Should().BeFalse();
        n.HasValue.Should().BeFalse();
        n.IsNullOrEmpty.Should().BeTrue();

        RedisValue emptyArr = Array.Empty<byte>();
        emptyArr.IsNull.Should().BeFalse();
        emptyArr.IsInteger.Should().BeFalse();
        emptyArr.HasValue.Should().BeFalse();
        emptyArr.IsNullOrEmpty.Should().BeTrue();
    }

    [Fact]
    public void from_stream()
    {
        var arr = Encoding.UTF8.GetBytes("hello world");
        var ms = new MemoryStream(arr);
        var val = RedisValue.CreateFrom(ms);
        val.Should().Be("hello world");

        ms = new MemoryStream(arr, 1, 6, false, false);
        val = RedisValue.CreateFrom(ms);
        val.Should().Be("ello w");

        ms = new MemoryStream(arr, 2, 6, false, true);
        val = RedisValue.CreateFrom(ms);
        val.Should().Be("llo wo");
    }
}

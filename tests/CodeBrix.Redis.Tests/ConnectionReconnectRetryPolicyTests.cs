using System;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class TransientErrorTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public void test_exponential_retry()
    {
        IReconnectRetryPolicy exponentialRetry = new ExponentialRetry(5000);
        exponentialRetry.ShouldRetry(0, 0).Should().BeFalse();
        exponentialRetry.ShouldRetry(1, 5600).Should().BeTrue();
        exponentialRetry.ShouldRetry(2, 6050).Should().BeTrue();
        exponentialRetry.ShouldRetry(2, 4050).Should().BeFalse();
    }

    [Fact]
    public void test_exponential_max_retry()
    {
        IReconnectRetryPolicy exponentialRetry = new ExponentialRetry(5000);
        exponentialRetry.ShouldRetry(long.MaxValue, (int)TimeSpan.FromSeconds(30).TotalMilliseconds).Should().BeTrue();
    }

    [Fact]
    public void test_exponential_retry_args()
    {
        _ = new ExponentialRetry(5000);
        _ = new ExponentialRetry(5000, 10000);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialRetry(-1));
        ex.ParamName.Should().Be("deltaBackOffMilliseconds");

        ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialRetry(5000, -1));
        ex.ParamName.Should().Be("maxDeltaBackOffMilliseconds");

        ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialRetry(10000, 5000));
        ex.ParamName.Should().Be("maxDeltaBackOffMilliseconds");
    }

    [Fact]
    public void test_linear_retry()
    {
        IReconnectRetryPolicy linearRetry = new LinearRetry(5000);
        linearRetry.ShouldRetry(0, 0).Should().BeFalse();
        linearRetry.ShouldRetry(2, 4999).Should().BeFalse();
        linearRetry.ShouldRetry(1, 5000).Should().BeTrue();
    }
}

using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class Misc(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Theory]
    [InlineData(":1\r\n", StreamTrimResult.Deleted)] // Integer 1
    [InlineData(":-1\r\n", StreamTrimResult.NotFound)] // Integer -1
    [InlineData(":2\r\n", StreamTrimResult.NotDeleted)] // Integer 2
    [InlineData("+1\r\n", StreamTrimResult.Deleted)] // Simple string "1"
    [InlineData("$1\r\n1\r\n", StreamTrimResult.Deleted)] // Bulk string "1"
    [InlineData("*1\r\n:1\r\n", StreamTrimResult.Deleted)] // Unit array with integer 1
    public void int32_enum_processor_stream_trim_result(string resp, StreamTrimResult expected)
    {
        //Arrange
        var processor = ResultProcessor.StreamTrimResult;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void int32_enum_array_processor_stream_trim_result_empty_array()
    {
        //Arrange
        var resp = "*0\r\n";
        var processor = ResultProcessor.StreamTrimResultArray;

        //Act
        var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
        result.Should().BeEmpty();
    }

    [Fact]
    public void int32_enum_array_processor_stream_trim_result_null_array()
    {
        //Arrange
        var resp = "*-1\r\n";
        var processor = ResultProcessor.StreamTrimResultArray;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }

    [Fact]
    public void int32_enum_array_processor_stream_trim_result_multiple_values()
    {
        // Array with 3 elements: [1, -1, 2]
        var resp = "*3\r\n:1\r\n:-1\r\n:2\r\n";
        var processor = ResultProcessor.StreamTrimResultArray;
        var result = Execute(resp, processor);
        Assert.NotNull(result);
        result.Length.Should().Be(3);
        result[0].Should().Be(StreamTrimResult.Deleted);
        result[1].Should().Be(StreamTrimResult.NotFound);
        result[2].Should().Be(StreamTrimResult.NotDeleted);
    }

    [Fact]
    public void connection_identity_processor_returns_end_point()
    {
        // ConnectionIdentityProcessor doesn't actually read from the RESP response,
        // it just returns the endpoint from the connection (or null if no bridge).
        var resp = "+OK\r\n";
        var processor = ResultProcessor.ConnectionIdentity;
        var result = Execute(resp, processor);

        // No bridge in test helper means result is null, but that's OK
        result.Should().BeNull();
    }

    [Fact]
    public void digest_processor_valid_digest()
    {
        // DigestProcessor reads a scalar string containing a hex digest
        // Example: XXh3 digest of "asdfasd" is "91d2544ff57ccca3"
        var resp = "$16\r\n91d2544ff57ccca3\r\n";
        var processor = ResultProcessor.Digest;
        var result = Execute(resp, processor);
        Assert.NotNull(result);
        result.HasValue.Should().BeTrue();

        // Parse the expected digest and verify equality
        var expected = ValueCondition.ParseDigest("91d2544ff57ccca3");
        result.Value.Should().Be(expected);
    }

    [Fact]
    public void digest_processor_null_digest()
    {
        // DigestProcessor should handle null responses
        var resp = "$-1\r\n";
        var processor = ResultProcessor.Digest;
        var result = Execute(resp, processor);
        result.Should().BeNull();
    }
}

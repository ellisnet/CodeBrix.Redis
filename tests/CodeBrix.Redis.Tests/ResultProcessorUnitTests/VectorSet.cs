using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class VectorSet(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Theory]
    [InlineData("*0\r\n")] // empty array
    [InlineData("*12\r\n$4\r\nsize\r\n:100\r\n$8\r\nvset-uid\r\n:42\r\n$9\r\nmax-level\r\n:5\r\n$10\r\nvector-dim\r\n:128\r\n$10\r\nquant-type\r\n$4\r\nint8\r\n$17\r\nhnsw-max-node-uid\r\n:99\r\n")] // full info with int8
    public void vector_set_info_valid_input(string resp)
    {
        //Arrange
        var processor = ResultProcessor.VectorSetInfo;

        //Act
        var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void vector_set_info_empty_array_returns_defaults()
    {
        // Empty array should return VectorSetInfo with default values
        var resp = "*0\r\n";
        var processor = ResultProcessor.VectorSetInfo;
        var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Value.Quantization.Should().Be(VectorSetQuantization.Unknown);
        result.Value.QuantizationRaw.Should().BeNull();
        result.Value.Dimension.Should().Be(0);
        result.Value.Length.Should().Be(0);
        result.Value.MaxLevel.Should().Be(0);
        result.Value.VectorSetUid.Should().Be(0);
        result.Value.HnswMaxNodeUid.Should().Be(0);
    }

    [Theory]
    [InlineData("*-1\r\n")] // null array (RESP2)
    [InlineData("_\r\n")] // null (RESP3)
    public void vector_set_info_null_array(string resp)
    {
        //Arrange
        var processor = ResultProcessor.VectorSetInfo;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }

    [Fact]
    public void vector_set_info_validates_content_int8()
    {
        // VINFO response with int8 quantization
        var resp = "*12\r\n$4\r\nsize\r\n:100\r\n$8\r\nvset-uid\r\n:42\r\n$9\r\nmax-level\r\n:5\r\n$10\r\nvector-dim\r\n:128\r\n$10\r\nquant-type\r\n$4\r\nint8\r\n$17\r\nhnsw-max-node-uid\r\n:99\r\n";
        var processor = ResultProcessor.VectorSetInfo;
        var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Value.Quantization.Should().Be(VectorSetQuantization.Int8);
        result.Value.QuantizationRaw.Should().BeNull();
        result.Value.Dimension.Should().Be(128);
        result.Value.Length.Should().Be(100);
        result.Value.MaxLevel.Should().Be(5);
        result.Value.VectorSetUid.Should().Be(42);
        result.Value.HnswMaxNodeUid.Should().Be(99);
    }

    [Fact]
    public void vector_set_info_skips_non_scalar_values()
    {
        // Response with a non-scalar value (array) that should be skipped
        // Format: size:100, unknown-field:[1,2,3], vset-uid:42
        var resp = "*6\r\n$4\r\nsize\r\n:100\r\n$13\r\nunknown-field\r\n*3\r\n:1\r\n:2\r\n:3\r\n$8\r\nvset-uid\r\n:42\r\n";
        var processor = ResultProcessor.VectorSetInfo;
        var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Value.Length.Should().Be(100);
        result.Value.VectorSetUid.Should().Be(42);
        // Other fields should have default values
        result.Value.Quantization.Should().Be(VectorSetQuantization.Unknown);
        result.Value.Dimension.Should().Be(0);
    }

    [Fact]
    public void vector_set_links_empty_array()
    {
        // VLINKS returns empty array
        var resp = "*0\r\n";
        var processor = ResultProcessor.VectorSetLinks;
        using var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Length.Should().Be(0);
    }

    [Theory]
    [InlineData("*-1\r\n")] // null array (RESP2)
    [InlineData("_\r\n")] // null (RESP3)
    public void vector_set_links_null_array(string resp)
    {
        //Arrange
        var processor = ResultProcessor.VectorSetLinks;

        //Act
        using var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
        result.Length.Should().Be(0);
    }

    [Fact]
    public void vector_set_links_single_nested_array()
    {
        // VLINKS returns [[element1]]
        var resp = "*1\r\n*1\r\n$8\r\nelement1\r\n";
        var processor = ResultProcessor.VectorSetLinks;
        using var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Length.Should().Be(1);
        result.Span[0].ToString().Should().Be("element1");
    }

    [Fact]
    public void vector_set_links_multiple_nested_arrays()
    {
        // VLINKS returns [[element1], [element2, element3], [element4]]
        var resp = "*3\r\n*1\r\n$8\r\nelement1\r\n*2\r\n$8\r\nelement2\r\n$8\r\nelement3\r\n*1\r\n$8\r\nelement4\r\n";
        var processor = ResultProcessor.VectorSetLinks;
        using var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Length.Should().Be(4);
        result.Span[0].ToString().Should().Be("element1");
        result.Span[1].ToString().Should().Be("element2");
        result.Span[2].ToString().Should().Be("element3");
        result.Span[3].ToString().Should().Be("element4");
    }

    [Fact]
    public void vector_set_links_with_scores_empty_array()
    {
        // VLINKS WITHSCORES returns empty array
        var resp = "*0\r\n";
        var processor = ResultProcessor.VectorSetLinksWithScores;
        using var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Length.Should().Be(0);
    }

    [Theory]
    [InlineData("*-1\r\n")] // null array (RESP2)
    [InlineData("_\r\n")] // null (RESP3)
    public void vector_set_links_with_scores_null_array(string resp)
    {
        //Arrange
        var processor = ResultProcessor.VectorSetLinksWithScores;

        //Act
        using var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
        result.Length.Should().Be(0);
    }

    [Fact]
    public void vector_set_links_with_scores_single_nested_array()
    {
        // VLINKS WITHSCORES returns [[element1, score1]]
        var resp = "*1\r\n*2\r\n$8\r\nelement1\r\n$3\r\n1.5\r\n";
        var processor = ResultProcessor.VectorSetLinksWithScores;
        using var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Length.Should().Be(1);
        result.Span[0].Member.ToString().Should().Be("element1");
        result.Span[0].Score.Should().Be(1.5);
    }

    [Fact]
    public void vector_set_links_with_scores_multiple_nested_arrays()
    {
        // VLINKS WITHSCORES returns [[element1, score1], [element2, score2], [element3, score3]]
        var resp = "*3\r\n*2\r\n$8\r\nelement1\r\n$3\r\n1.5\r\n*2\r\n$8\r\nelement2\r\n$3\r\n2.5\r\n*2\r\n$8\r\nelement3\r\n$3\r\n3.5\r\n";
        var processor = ResultProcessor.VectorSetLinksWithScores;
        using var result = Execute(resp, processor);

        Assert.NotNull(result);
        result.Length.Should().Be(3);
        result.Span[0].Member.ToString().Should().Be("element1");
        result.Span[0].Score.Should().Be(1.5);
        result.Span[1].Member.ToString().Should().Be("element2");
        result.Span[1].Score.Should().Be(2.5);
        result.Span[2].Member.ToString().Should().Be("element3");
        result.Span[2].Score.Should().Be(3.5);
    }
}

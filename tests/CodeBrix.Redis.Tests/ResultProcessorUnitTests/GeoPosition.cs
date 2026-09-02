using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class GeoPosition(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void geo_position_valid_position_returns_geo_position()
    {
        //Arrange
        var resp = "*1\r\n*2\r\n$18\r\n13.361389338970184\r\n$16\r\n38.1155563954963\r\n";
        var processor = ResultProcessor.RedisGeoPosition;

        //Act
        var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
        result.Value.Longitude.Should().BeApproximately(13.361389338970184, 1e-10);
        result.Value.Latitude.Should().BeApproximately(38.1155563954963, 1e-10);
    }

    [Fact]
    public void geo_position_null_element_returns_null()
    {
        //Arrange
        var resp = "*1\r\n$-1\r\n";
        var processor = ResultProcessor.RedisGeoPosition;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }

    [Fact]
    public void geo_position_empty_array_returns_null()
    {
        //Arrange
        var resp = "*0\r\n";
        var processor = ResultProcessor.RedisGeoPosition;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }

    [Fact]
    public void geo_position_null_array_returns_null()
    {
        //Arrange
        var resp = "*-1\r\n";
        var processor = ResultProcessor.RedisGeoPosition;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }

    [Fact]
    public void geo_position_integer_coordinates_returns_geo_position()
    {
        //Arrange
        var resp = "*1\r\n*2\r\n:13\r\n:38\r\n";
        var processor = ResultProcessor.RedisGeoPosition;

        //Act
        var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
        result.Value.Longitude.Should().Be(13.0);
        result.Value.Latitude.Should().Be(38.0);
    }

    [Fact]
    public void geo_position_array_multiple_positions_returns_array()
    {
        //Arrange
        var resp = "*3\r\n" +
                   "*2\r\n$18\r\n13.361389338970184\r\n$16\r\n38.1155563954963\r\n" +
                   "*2\r\n$18\r\n15.087267458438873\r\n$17\r\n37.50266842333162\r\n" +
                   "$-1\r\n";
        var processor = ResultProcessor.RedisGeoPositionArray;

        //Act
        var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
        result.Length.Should().Be(3);

        Assert.NotNull(result[0]);
        result[0]!.Value.Longitude.Should().BeApproximately(13.361389338970184, 1e-10);
        result[0]!.Value.Latitude.Should().BeApproximately(38.1155563954963, 1e-10);

        Assert.NotNull(result[1]);
        result[1]!.Value.Longitude.Should().BeApproximately(15.087267458438873, 1e-10);
        result[1]!.Value.Latitude.Should().BeApproximately(37.50266842333162, 1e-10);

        result[2].Should().BeNull();
    }

    [Fact]
    public void geo_position_array_empty_array_returns_empty_array()
    {
        //Arrange
        var resp = "*0\r\n";
        var processor = ResultProcessor.RedisGeoPositionArray;

        //Act
        var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
        result.Should().BeEmpty();
    }

    [Fact]
    public void geo_position_array_null_array_returns_null()
    {
        //Arrange
        var resp = "*-1\r\n";
        var processor = ResultProcessor.RedisGeoPositionArray;

        //Act
        var result = Execute(resp, processor);

        //Assert
        result.Should().BeNull();
    }

    [Fact]
    public void geo_position_array_all_nulls_returns_array_of_nulls()
    {
        //Arrange
        var resp = "*2\r\n$-1\r\n$-1\r\n";
        var processor = ResultProcessor.RedisGeoPositionArray;

        //Act
        var result = Execute(resp, processor);

        //Assert
        Assert.NotNull(result);
        result.Length.Should().Be(2);
        result[0].Should().BeNull();
        result[1].Should().BeNull();
    }
}

using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.ResultProcessorUnitTests; //was previously: StackExchange.Redis.Tests.ResultProcessorUnitTests;

public class GeoRadius(ITestOutputHelper log) : ResultProcessorUnitTest(log)
{
    [Fact]
    public void geo_radius_none_returns_just_members()
    {
        // Without any WITH option: just member names as scalars in array
        var resp = "*2\r\n$7\r\nPalermo\r\n$7\r\nCatania\r\n";
        var result = Execute<GeoRadiusResult[]>(resp, ResultProcessor.GeoRadiusArray(GeoRadiusOptions.None));

        Assert.NotNull(result);
        result.Length.Should().Be(2);
        result[0].Member.Should().Be("Palermo");
        result[0].Distance.Should().BeNull();
        result[0].Hash.Should().BeNull();
        result[0].Position.Should().BeNull();
        result[1].Member.Should().Be("Catania");
        result[1].Distance.Should().BeNull();
        result[1].Hash.Should().BeNull();
        result[1].Position.Should().BeNull();
    }

    [Fact]
    public void geo_radius_with_distance_returns_distances()
    {
        // With WITHDIST: each element is [member, distance]
        var resp = "*2\r\n" +
                   "*2\r\n$7\r\nPalermo\r\n$8\r\n190.4424\r\n" +
                   "*2\r\n$7\r\nCatania\r\n$7\r\n56.4413\r\n";
        var result = Execute<GeoRadiusResult[]>(resp, ResultProcessor.GeoRadiusArray(GeoRadiusOptions.WithDistance));

        Assert.NotNull(result);
        result.Length.Should().Be(2);
        result[0].Member.Should().Be("Palermo");
        result[0].Distance.Should().Be(190.4424);
        result[0].Hash.Should().BeNull();
        result[0].Position.Should().BeNull();
        result[1].Member.Should().Be("Catania");
        result[1].Distance.Should().Be(56.4413);
        result[1].Hash.Should().BeNull();
        result[1].Position.Should().BeNull();
    }

    [Fact]
    public void geo_radius_with_coordinates_returns_positions()
    {
        // With WITHCOORD: each element is [member, [longitude, latitude]]
        var resp = "*2\r\n" +
                   "*2\r\n$7\r\nPalermo\r\n*2\r\n$18\r\n13.361389338970184\r\n$16\r\n38.1155563954963\r\n" +
                   "*2\r\n$7\r\nCatania\r\n*2\r\n$18\r\n15.087267458438873\r\n$17\r\n37.50266842333162\r\n";
        var result = Execute<GeoRadiusResult[]>(resp, ResultProcessor.GeoRadiusArray(GeoRadiusOptions.WithCoordinates));

        Assert.NotNull(result);
        result.Length.Should().Be(2);
        result[0].Member.Should().Be("Palermo");
        result[0].Distance.Should().BeNull();
        result[0].Hash.Should().BeNull();
        Assert.NotNull(result[0].Position);
        result[0].Position!.Value.Longitude.Should().Be(13.361389338970184);
        result[0].Position!.Value.Latitude.Should().Be(38.1155563954963);
        result[1].Member.Should().Be("Catania");
        result[1].Distance.Should().BeNull();
        result[1].Hash.Should().BeNull();
        Assert.NotNull(result[1].Position);
        result[1].Position!.Value.Longitude.Should().Be(15.087267458438873);
        result[1].Position!.Value.Latitude.Should().Be(37.50266842333162);
    }

    [Fact]
    public void geo_radius_with_distance_and_coordinates_returns_both()
    {
        // With WITHDIST WITHCOORD: each element is [member, distance, [longitude, latitude]]
        var resp = "*2\r\n" +
                   "*3\r\n$7\r\nPalermo\r\n$8\r\n190.4424\r\n*2\r\n$18\r\n13.361389338970184\r\n$16\r\n38.1155563954963\r\n" +
                   "*3\r\n$7\r\nCatania\r\n$7\r\n56.4413\r\n*2\r\n$18\r\n15.087267458438873\r\n$17\r\n37.50266842333162\r\n";
        var result = Execute<GeoRadiusResult[]>(resp, ResultProcessor.GeoRadiusArray(GeoRadiusOptions.WithDistance | GeoRadiusOptions.WithCoordinates));

        Assert.NotNull(result);
        result.Length.Should().Be(2);
        result[0].Member.Should().Be("Palermo");
        result[0].Distance.Should().Be(190.4424);
        result[0].Hash.Should().BeNull();
        Assert.NotNull(result[0].Position);
        result[0].Position!.Value.Longitude.Should().Be(13.361389338970184);
        result[0].Position!.Value.Latitude.Should().Be(38.1155563954963);
    }

    [Fact]
    public void geo_radius_with_hash_returns_hash()
    {
        // With WITHHASH: each element is [member, hash]
        var resp = "*2\r\n" +
                   "*2\r\n$7\r\nPalermo\r\n:3479099956230698\r\n" +
                   "*2\r\n$7\r\nCatania\r\n:3479447370796909\r\n";
        var result = Execute<GeoRadiusResult[]>(resp, ResultProcessor.GeoRadiusArray(GeoRadiusOptions.WithGeoHash));

        Assert.NotNull(result);
        result.Length.Should().Be(2);
        result[0].Member.Should().Be("Palermo");
        result[0].Distance.Should().BeNull();
        result[0].Hash.Should().Be(3479099956230698);
        result[0].Position.Should().BeNull();
        result[1].Member.Should().Be("Catania");
        result[1].Distance.Should().BeNull();
        result[1].Hash.Should().Be(3479447370796909);
        result[1].Position.Should().BeNull();
    }

    [Fact]
    public void geo_radius_all_options_returns_everything()
    {
        // With all options: [member, distance, hash, [longitude, latitude]]
        var resp = "*1\r\n" +
                   "*4\r\n$7\r\nPalermo\r\n$8\r\n190.4424\r\n:3479099956230698\r\n*2\r\n$18\r\n13.361389338970184\r\n$16\r\n38.1155563954963\r\n";
        var result = Execute<GeoRadiusResult[]>(
            resp,
            ResultProcessor.GeoRadiusArray(GeoRadiusOptions.WithDistance | GeoRadiusOptions.WithGeoHash | GeoRadiusOptions.WithCoordinates));

        Assert.NotNull(result);
        result.Should().ContainSingle();
        result[0].Member.Should().Be("Palermo");
        result[0].Distance.Should().Be(190.4424);
        result[0].Hash.Should().Be(3479099956230698);
        Assert.NotNull(result[0].Position);
        result[0].Position!.Value.Longitude.Should().Be(13.361389338970184);
        result[0].Position!.Value.Latitude.Should().Be(38.1155563954963);
    }

    [Fact]
    public void geo_radius_empty_array_returns_empty_array()
    {
        //Arrange
        var resp = "*0\r\n";

        //Act
        var result = Execute<GeoRadiusResult[]>(resp, ResultProcessor.GeoRadiusArray(GeoRadiusOptions.None));

        //Assert
        Assert.NotNull(result);
        result.Should().BeEmpty();
    }
}

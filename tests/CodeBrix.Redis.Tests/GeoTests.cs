using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class GeoTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    private static readonly GeoEntry
        Palermo = new GeoEntry(13.361389, 38.115556, "Palermo"),
        Catania = new GeoEntry(15.087269, 37.502669, "Catania"),
        Agrigento = new GeoEntry(13.5765, 37.311, "Agrigento"),
        Cefalù = new GeoEntry(14.0188, 38.0084, "Cefalù");

    private static readonly GeoEntry[] All = [Palermo, Catania, Agrigento, Cefalù];

    [Fact]
    public async Task geo_add()
    {
        await using var conn = Create(require: RedisFeatures.v3_2_0);

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        // add while not there
        db.GeoAdd(key, Cefalù.Longitude, Cefalù.Latitude, Cefalù.Member).Should().BeTrue();
        db.GeoAdd(key, [Palermo, Catania]).Should().Be(2);
        db.GeoAdd(key, Agrigento).Should().BeTrue();

        // now add again
        db.GeoAdd(key, Cefalù.Longitude, Cefalù.Latitude, Cefalù.Member).Should().BeFalse();
        db.GeoAdd(key, [Palermo, Catania]).Should().Be(0);
        db.GeoAdd(key, Agrigento).Should().BeFalse();

        // Validate
        var pos = db.GeoPosition(key, Palermo.Member);
        Assert.NotNull(pos);
        pos!.Value.Longitude.Should().BeApproximately(Palermo.Longitude, 0.000005);
        pos!.Value.Latitude.Should().BeApproximately(Palermo.Latitude, 0.000005);
    }

    [Fact]
    public async Task get_distance()
    {
        await using var conn = Create(require: RedisFeatures.v3_2_0);

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.GeoAdd(key, All, CommandFlags.FireAndForget);
        var val = db.GeoDistance(key, "Palermo", "Catania", GeoUnit.Meters);
        val.HasValue.Should().BeTrue();
        val.Should().Be(166274.1516);

        val = db.GeoDistance(key, "Palermo", "Nowhere", GeoUnit.Meters);
        val.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task geo_hash()
    {
        await using var conn = Create(require: RedisFeatures.v3_2_0);

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.GeoAdd(key, All, CommandFlags.FireAndForget);

        var hashes = db.GeoHash(key, [Palermo.Member, "Nowhere", Agrigento.Member]);
        Assert.NotNull(hashes);
        hashes.Length.Should().Be(3);
        hashes[0].Should().Be("sqc8b49rny0");
        hashes[1].Should().BeNull();
        hashes[2].Should().Be("sq9skbq0760");

        var hash = db.GeoHash(key, "Palermo");
        hash.Should().Be("sqc8b49rny0");

        hash = db.GeoHash(key, "Nowhere");
        hash.Should().BeNull();
    }

    [Fact]
    public async Task geo_get_position()
    {
        await using var conn = Create(require: RedisFeatures.v3_2_0);

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.GeoAdd(key, All, CommandFlags.FireAndForget);

        var pos = db.GeoPosition(key, Palermo.Member);
        Assert.True(pos.HasValue); //kept as the xUnit form: it is annotated so the compiler's null-state flows to the use below
        Math.Round(pos.Value.Longitude, 6).Should().Be(Math.Round(Palermo.Longitude, 6));
        Math.Round(pos.Value.Latitude, 6).Should().Be(Math.Round(Palermo.Latitude, 6));

        pos = db.GeoPosition(key, "Nowhere");
        pos.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task geo_remove()
    {
        await using var conn = Create(require: RedisFeatures.v3_2_0);

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.GeoAdd(key, All, CommandFlags.FireAndForget);

        var pos = db.GeoPosition(key, "Palermo");
        Assert.True(pos.HasValue); //kept as the xUnit form: it is annotated so the compiler's null-state flows to the use below

        db.GeoRemove(key, "Nowhere").Should().BeFalse();
        db.GeoRemove(key, "Palermo").Should().BeTrue();
        db.GeoRemove(key, "Palermo").Should().BeFalse();

        pos = db.GeoPosition(key, "Palermo");
        pos.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task geo_radius()
    {
        await using var conn = Create(require: RedisFeatures.v3_2_0);

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.GeoAdd(key, All, CommandFlags.FireAndForget);

        var results = db.GeoRadius(key, Cefalù.Member, 60, GeoUnit.Miles, 2, Order.Ascending);
        results.Length.Should().Be(2);

        (Cefalù.Member).Should().Be(results[0].Member);
        results[0].Distance.Should().Be(0);
        var position0 = results[0].Position;
        Assert.NotNull(position0);
        Math.Round(Cefalù.Position.Longitude, 5).Should().Be(Math.Round(position0!.Value.Longitude, 5));
        Math.Round(Cefalù.Position.Latitude, 5).Should().Be(Math.Round(position0!.Value.Latitude, 5));
        results[0].Hash.HasValue.Should().BeFalse();

        Palermo.Member.Should().Be(results[1].Member);
        var distance1 = results[1].Distance;
        Assert.NotNull(distance1);
        Math.Round(distance1!.Value, 6).Should().Be(Math.Round(36.5319, 6));
        var position1 = results[1].Position;
        Assert.NotNull(position1);
        Math.Round(Palermo.Position.Longitude, 5).Should().Be(Math.Round(position1!.Value.Longitude, 5));
        Math.Round(Palermo.Position.Latitude, 5).Should().Be(Math.Round(position1!.Value.Latitude, 5));
        results[1].Hash.HasValue.Should().BeFalse();

        results = db.GeoRadius(key, Cefalù.Member, 60, GeoUnit.Miles, 2, Order.Ascending, GeoRadiusOptions.None);
        results.Length.Should().Be(2);
        (Cefalù.Member).Should().Be(results[0].Member);
        results[0].Position.HasValue.Should().BeFalse();
        results[0].Distance.HasValue.Should().BeFalse();
        results[0].Hash.HasValue.Should().BeFalse();

        Palermo.Member.Should().Be(results[1].Member);
        results[1].Position.HasValue.Should().BeFalse();
        results[1].Distance.HasValue.Should().BeFalse();
        results[1].Hash.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task geo_radius_overloads()
    {
        await using var conn = Create(require: RedisFeatures.v3_2_0);

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);

        db.GeoAdd(key, -1.759925, 52.19493, "steve").Should().BeTrue();
        db.GeoAdd(key, -3.360655, 54.66395, "dave").Should().BeTrue();

        // Invalid overload
        // Since this would throw ERR could not decode requested zset member, we catch and return something more useful to the user earlier.
        var ex = Assert.Throws<ArgumentException>(() => db.GeoRadius(key, -1.759925, 52.19493, GeoUnit.Miles, 500, Order.Ascending, GeoRadiusOptions.WithDistance));
        ex.Message.Should().StartWith("Member should not be a double, you likely want the GeoRadius(RedisKey, double, double, ...) overload.");
        ex.ParamName.Should().Be("member");
        ex = await Assert.ThrowsAsync<ArgumentException>(() => db.GeoRadiusAsync(key, -1.759925, 52.19493, GeoUnit.Miles, 500, Order.Ascending, GeoRadiusOptions.WithDistance)).ForAwait();
        ex.Message.Should().StartWith("Member should not be a double, you likely want the GeoRadius(RedisKey, double, double, ...) overload.");
        ex.ParamName.Should().Be("member");

        // The good stuff
        GeoRadiusResult[] result = db.GeoRadius(key, -1.759925, 52.19493, 500, unit: GeoUnit.Miles, order: Order.Ascending, options: GeoRadiusOptions.WithDistance);
        Assert.NotNull(result);
        result = await db.GeoRadiusAsync(key, -1.759925, 52.19493, 500, unit: GeoUnit.Miles, order: Order.Ascending, options: GeoRadiusOptions.WithDistance).ForAwait();
        Assert.NotNull(result);
    }

    private async Task GeoSearchSetupAsync(RedisKey key, IDatabase db)
    {
        await db.KeyDeleteAsync(key);
        await db.GeoAddAsync(key, 82.6534, 27.7682, "rays");
        await db.GeoAddAsync(key, 79.3891, 43.6418, "blue jays");
        await db.GeoAddAsync(key, 76.6217, 39.2838, "orioles");
        await db.GeoAddAsync(key, 71.0927, 42.3467, "red sox");
        await db.GeoAddAsync(key, 73.9262, 40.8296, "yankees");
    }

    private void GeoSearchSetup(RedisKey key, IDatabase db)
    {
        db.KeyDelete(key);
        db.GeoAdd(key, 82.6534, 27.7682, "rays");
        db.GeoAdd(key, 79.3891, 43.6418, "blue jays");
        db.GeoAdd(key, 76.6217, 39.2838, "orioles");
        db.GeoAdd(key, 71.0927, 42.3467, "red sox");
        db.GeoAdd(key, 73.9262, 40.8296, "yankees");
    }

    [Fact]
    public async Task geo_search_circle_member_async()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var key = Me();
        var db = conn.GetDatabase();
        await GeoSearchSetupAsync(key, db);
        var circle = new GeoSearchCircle(500, GeoUnit.Miles);

        //Act
        var res = await db.GeoSearchAsync(key, "yankees", circle);

        //Assert
        res.Should().Contain(x => x.Member == "yankees");
        res.Should().Contain(x => x.Member == "red sox");
        res.Should().Contain(x => x.Member == "orioles");
        res.Should().Contain(x => x.Member == "blue jays");
        Assert.NotNull(res[0].Distance);
        Assert.NotNull(res[0].Position);
        res[0].Hash.Should().BeNull();
        res.Length.Should().Be(4);
    }

    [Fact]
    public async Task geo_search_circle_member_async_only_hash()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var key = Me();
        var db = conn.GetDatabase();
        await GeoSearchSetupAsync(key, db);
        var circle = new GeoSearchCircle(500, GeoUnit.Miles);

        //Act
        var res = await db.GeoSearchAsync(key, "yankees", circle, options: GeoRadiusOptions.WithGeoHash);

        //Assert
        res.Should().Contain(x => x.Member == "yankees");
        res.Should().Contain(x => x.Member == "red sox");
        res.Should().Contain(x => x.Member == "orioles");
        res.Should().Contain(x => x.Member == "blue jays");
        res[0].Distance.Should().BeNull();
        res[0].Position.Should().BeNull();
        Assert.NotNull(res[0].Hash);
        res.Length.Should().Be(4);
    }

    [Fact]
    public async Task geo_search_circle_member_async_hash_and_distance()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var key = Me();
        var db = conn.GetDatabase();
        await GeoSearchSetupAsync(key, db);
        var circle = new GeoSearchCircle(500, GeoUnit.Miles);

        //Act
        var res = await db.GeoSearchAsync(key, "yankees", circle, options: GeoRadiusOptions.WithGeoHash | GeoRadiusOptions.WithDistance);

        //Assert
        res.Should().Contain(x => x.Member == "yankees");
        res.Should().Contain(x => x.Member == "red sox");
        res.Should().Contain(x => x.Member == "orioles");
        res.Should().Contain(x => x.Member == "blue jays");
        Assert.NotNull(res[0].Distance);
        res[0].Position.Should().BeNull();
        Assert.NotNull(res[0].Hash);
        res.Length.Should().Be(4);
    }

    [Fact]
    public async Task geo_search_circle_lon_lat_async()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var key = Me();
        var db = conn.GetDatabase();
        await GeoSearchSetupAsync(key, db);
        var circle = new GeoSearchCircle(500, GeoUnit.Miles);

        //Act
        var res = await db.GeoSearchAsync(key, 73.9262, 40.8296, circle);

        //Assert
        res.Should().Contain(x => x.Member == "yankees");
        res.Should().Contain(x => x.Member == "red sox");
        res.Should().Contain(x => x.Member == "orioles");
        res.Should().Contain(x => x.Member == "blue jays");
        res.Length.Should().Be(4);
    }

    [Fact]
    public async Task geo_search_circle_member()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var key = Me();
        var db = conn.GetDatabase();
        GeoSearchSetup(key, db);
        var circle = new GeoSearchCircle(500 * 1609);

        //Act
        var res = db.GeoSearch(key, "yankees", circle);

        //Assert
        res.Should().Contain(x => x.Member == "yankees");
        res.Should().Contain(x => x.Member == "red sox");
        res.Should().Contain(x => x.Member == "orioles");
        res.Should().Contain(x => x.Member == "blue jays");
        res.Length.Should().Be(4);
    }

    [Fact]
    public async Task geo_search_circle_lon_lat()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var key = Me();
        var db = conn.GetDatabase();
        GeoSearchSetup(key, db);
        var circle = new GeoSearchCircle(500 * 5280, GeoUnit.Feet);

        //Act
        var res = db.GeoSearch(key, 73.9262, 40.8296, circle);

        //Assert
        res.Should().Contain(x => x.Member == "yankees");
        res.Should().Contain(x => x.Member == "red sox");
        res.Should().Contain(x => x.Member == "orioles");
        res.Should().Contain(x => x.Member == "blue jays");
        res.Length.Should().Be(4);
    }

    [Fact]
    public async Task geo_search_box_member_async()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var key = Me();
        var db = conn.GetDatabase();
        await GeoSearchSetupAsync(key, db);
        var box = new GeoSearchBox(500, 500, GeoUnit.Kilometers);

        //Act
        var res = await db.GeoSearchAsync(key, "yankees", box);

        //Assert
        res.Should().Contain(x => x.Member == "yankees");
        res.Should().Contain(x => x.Member == "red sox");
        res.Should().Contain(x => x.Member == "orioles");
        res.Length.Should().Be(3);
    }

    [Fact]
    public async Task geo_search_box_lon_lat_async()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var key = Me();
        var db = conn.GetDatabase();
        await GeoSearchSetupAsync(key, db);
        var box = new GeoSearchBox(500, 500, GeoUnit.Kilometers);

        //Act
        var res = await db.GeoSearchAsync(key, 73.9262, 40.8296, box);

        //Assert
        res.Should().Contain(x => x.Member == "yankees");
        res.Should().Contain(x => x.Member == "red sox");
        res.Should().Contain(x => x.Member == "orioles");
        res.Length.Should().Be(3);
    }

    [Fact]
    public async Task geo_search_box_member()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var key = Me();
        var db = conn.GetDatabase();
        GeoSearchSetup(key, db);
        var box = new GeoSearchBox(500, 500, GeoUnit.Kilometers);

        //Act
        var res = db.GeoSearch(key, "yankees", box);

        //Assert
        res.Should().Contain(x => x.Member == "yankees");
        res.Should().Contain(x => x.Member == "red sox");
        res.Should().Contain(x => x.Member == "orioles");
        res.Length.Should().Be(3);
    }

    [Fact]
    public async Task geo_search_box_lon_lat()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var key = Me();
        var db = conn.GetDatabase();
        GeoSearchSetup(key, db);
        var box = new GeoSearchBox(500, 500, GeoUnit.Kilometers);

        //Act
        var res = db.GeoSearch(key, 73.9262, 40.8296, box);

        //Assert
        res.Should().Contain(x => x.Member == "yankees");
        res.Should().Contain(x => x.Member == "red sox");
        res.Should().Contain(x => x.Member == "orioles");
        res.Length.Should().Be(3);
    }

    [Fact]
    public async Task geo_search_limit_count()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var key = Me();
        var db = conn.GetDatabase();
        GeoSearchSetup(key, db);
        var box = new GeoSearchBox(500, 500, GeoUnit.Kilometers);

        //Act
        var res = db.GeoSearch(key, 73.9262, 40.8296, box, count: 2);

        //Assert
        res.Should().Contain(x => x.Member == "yankees");
        res.Should().Contain(x => x.Member == "orioles");
        res.Length.Should().Be(2);
    }

    [Fact]
    public async Task geo_search_limit_count_make_no_demands()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        GeoSearchSetup(key, db);

        var box = new GeoSearchBox(500, 500, GeoUnit.Kilometers);
        var res = db.GeoSearch(key, 73.9262, 40.8296, box, count: 2, demandClosest: false);
        res.Should().Contain(x => x.Member == "red sox"); // this order MIGHT not be fully deterministic, seems to work for our purposes.
        res.Should().Contain(x => x.Member == "orioles");
        res.Length.Should().Be(2);
    }

    [Fact]
    public async Task geo_search_box_lon_lat_descending()
    {
        //Arrange
        await using var conn = Create(require: RedisFeatures.v6_2_0);
        var key = Me();
        var db = conn.GetDatabase();
        await GeoSearchSetupAsync(key, db);
        var box = new GeoSearchBox(500, 500, GeoUnit.Kilometers);

        //Act
        var res = await db.GeoSearchAsync(key, 73.9262, 40.8296, box, order: Order.Descending);

        //Assert
        res.Should().Contain(x => x.Member == "yankees");
        res.Should().Contain(x => x.Member == "red sox");
        res.Should().Contain(x => x.Member == "orioles");
        res.Length.Should().Be(3);
        res[0].Member.Should().Be("red sox");
    }

    [Fact]
    public async Task geo_search_box_member_and_store_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var me = Me();
        var db = conn.GetDatabase();
        RedisKey sourceKey = $"{me}:source";
        RedisKey destinationKey = $"{me}:destination";
        await db.KeyDeleteAsync(destinationKey);
        await GeoSearchSetupAsync(sourceKey, db);

        var box = new GeoSearchBox(500, 500, GeoUnit.Kilometers);
        var res = await db.GeoSearchAndStoreAsync(sourceKey, destinationKey, "yankees", box);
        var set = await db.GeoSearchAsync(destinationKey, "yankees", new GeoSearchCircle(10000, GeoUnit.Miles));
        set.Should().Contain(x => x.Member == "yankees");
        set.Should().Contain(x => x.Member == "red sox");
        set.Should().Contain(x => x.Member == "orioles");
        set.Length.Should().Be(3);
        res.Should().Be(3);
    }

    [Fact]
    public async Task geo_search_box_lon_lat_and_store_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var me = Me();
        var db = conn.GetDatabase();
        RedisKey sourceKey = $"{me}:source";
        RedisKey destinationKey = $"{me}:destination";
        await db.KeyDeleteAsync(destinationKey);
        await GeoSearchSetupAsync(sourceKey, db);

        var box = new GeoSearchBox(500, 500, GeoUnit.Kilometers);
        var res = await db.GeoSearchAndStoreAsync(sourceKey, destinationKey, 73.9262, 40.8296, box);
        var set = await db.GeoSearchAsync(destinationKey, "yankees", new GeoSearchCircle(10000, GeoUnit.Miles));
        set.Should().Contain(x => x.Member == "yankees");
        set.Should().Contain(x => x.Member == "red sox");
        set.Should().Contain(x => x.Member == "orioles");
        set.Length.Should().Be(3);
        res.Should().Be(3);
    }

    [Fact]
    public async Task geo_search_circle_member_and_store_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var me = Me();
        var db = conn.GetDatabase();
        RedisKey sourceKey = $"{me}:source";
        RedisKey destinationKey = $"{me}:destination";
        await db.KeyDeleteAsync(destinationKey);
        await GeoSearchSetupAsync(sourceKey, db);

        var circle = new GeoSearchCircle(500, GeoUnit.Kilometers);
        var res = await db.GeoSearchAndStoreAsync(sourceKey, destinationKey, "yankees", circle);
        var set = await db.GeoSearchAsync(destinationKey, "yankees", new GeoSearchCircle(10000, GeoUnit.Miles));
        set.Should().Contain(x => x.Member == "yankees");
        set.Should().Contain(x => x.Member == "red sox");
        set.Should().Contain(x => x.Member == "orioles");
        set.Length.Should().Be(3);
        res.Should().Be(3);
    }

    [Fact]
    public async Task geo_search_circle_lon_lat_and_store_async()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var me = Me();
        var db = conn.GetDatabase();
        RedisKey sourceKey = $"{me}:source";
        RedisKey destinationKey = $"{me}:destination";
        await db.KeyDeleteAsync(destinationKey);
        await GeoSearchSetupAsync(sourceKey, db);

        var circle = new GeoSearchCircle(500, GeoUnit.Kilometers);
        var res = await db.GeoSearchAndStoreAsync(sourceKey, destinationKey, 73.9262, 40.8296, circle);
        var set = await db.GeoSearchAsync(destinationKey, "yankees", new GeoSearchCircle(10000, GeoUnit.Miles));
        set.Should().Contain(x => x.Member == "yankees");
        set.Should().Contain(x => x.Member == "red sox");
        set.Should().Contain(x => x.Member == "orioles");
        set.Length.Should().Be(3);
        res.Should().Be(3);
    }

    [Fact]
    public async Task geo_search_circle_member_and_store()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var me = Me();
        var db = conn.GetDatabase();
        RedisKey sourceKey = $"{me}:source";
        RedisKey destinationKey = $"{me}:destination";
        db.KeyDelete(destinationKey);
        GeoSearchSetup(sourceKey, db);

        var circle = new GeoSearchCircle(500, GeoUnit.Kilometers);
        var res = db.GeoSearchAndStore(sourceKey, destinationKey, "yankees", circle);
        var set = db.GeoSearch(destinationKey, "yankees", new GeoSearchCircle(10000, GeoUnit.Miles));
        set.Should().Contain(x => x.Member == "yankees");
        set.Should().Contain(x => x.Member == "red sox");
        set.Should().Contain(x => x.Member == "orioles");
        set.Length.Should().Be(3);
        res.Should().Be(3);
    }

    [Fact]
    public async Task geo_search_circle_lon_lat_and_store()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var me = Me();
        var db = conn.GetDatabase();
        RedisKey sourceKey = $"{me}:source";
        RedisKey destinationKey = $"{me}:destination";
        db.KeyDelete(destinationKey);
        GeoSearchSetup(sourceKey, db);

        var circle = new GeoSearchCircle(500, GeoUnit.Kilometers);
        var res = db.GeoSearchAndStore(sourceKey, destinationKey, 73.9262, 40.8296, circle);
        var set = db.GeoSearch(destinationKey, "yankees", new GeoSearchCircle(10000, GeoUnit.Miles));
        set.Should().Contain(x => x.Member == "yankees");
        set.Should().Contain(x => x.Member == "red sox");
        set.Should().Contain(x => x.Member == "orioles");
        set.Length.Should().Be(3);
        res.Should().Be(3);
    }

    [Fact]
    public async Task geo_search_circle_and_store_dist_only()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var me = Me();
        var db = conn.GetDatabase();
        RedisKey sourceKey = $"{me}:source";
        RedisKey destinationKey = $"{me}:destination";
        db.KeyDelete(destinationKey);
        GeoSearchSetup(sourceKey, db);

        var circle = new GeoSearchCircle(500, GeoUnit.Kilometers);
        var res = db.GeoSearchAndStore(sourceKey, destinationKey, 73.9262, 40.8296, circle, storeDistances: true);
        var set = db.SortedSetRangeByRankWithScores(destinationKey);
        set.Should().Contain(x => x.Element == "yankees");
        set.Should().Contain(x => x.Element == "red sox");
        set.Should().Contain(x => x.Element == "orioles");
        Array.Find(set, x => x.Element == "yankees").Score.Should().BeInRange(0, .2);
        Array.Find(set, x => x.Element == "orioles").Score.Should().BeInRange(286, 287);
        Array.Find(set, x => x.Element == "red sox").Score.Should().BeInRange(289, 290);
        set.Length.Should().Be(3);
        res.Should().Be(3);
    }

    [Fact]
    public async Task geo_search_bad_args()
    {
        await using var conn = Create(require: RedisFeatures.v6_2_0);

        var key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key);
        var circle = new GeoSearchCircle(500, GeoUnit.Kilometers);
        var exception = Assert.Throws<ArgumentException>(() =>
            db.GeoSearch(key, "irrelevant", circle, demandClosest: false));

        exception.Message.Should().Contain("demandClosest must be true if you are not limiting the count for a GEOSEARCH");
    }
}

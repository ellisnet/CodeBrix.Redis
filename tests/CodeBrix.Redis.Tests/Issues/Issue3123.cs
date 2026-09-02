using System;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class Issue3123(ITestOutputHelper output, SharedConnectionFixture? fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task run()
    {
        await using var conn = Create();
        var db = conn.GetDatabase();
        var key = Me();
        await db.KeyDeleteAsync(key, flags: CommandFlags.FireAndForget);

        Guid guid = Guid.NewGuid();
        byte[] payload = guid.ToByteArray();

        await db.GeoAddAsync(
            key,
            longitude: -77.0365,
            latitude: 38.8977,
            member: payload,
            flags: CommandFlags.FireAndForget);

        GeoSearchCircle commonSearchCircle = new(1, GeoUnit.Kilometers);

        GeoRadiusResult[] results =
            await db.GeoSearchAsync(
                key,
                longitude: -77.0365,
                latitude: 38.8977,
                shape: commonSearchCircle);
        var result = Assert.Single(results);

        byte[] final = (byte[])result.Member!;
        final.SequenceEqual(payload).Should().BeTrue();
        new Guid(final).Should().Be(guid);
    }
}

using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class MemoryTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task can_call_doctor()
    {
        await using var conn = Create(require: RedisFeatures.v4_0_0);

        var server = conn.GetServer(conn.GetEndPoints()[0]);
        string? doctor = server.MemoryDoctor();
        doctor.Should().NotBeNull();
        doctor.Should().NotBe("");

        doctor = await server.MemoryDoctorAsync();
        doctor.Should().NotBeNull();
        doctor.Should().NotBe("");
    }

    [Fact]
    public async Task can_purge()
    {
        await using var conn = Create(require: RedisFeatures.v4_0_0);

        var server = conn.GetServer(conn.GetEndPoints()[0]);
        server.MemoryPurge();
        await server.MemoryPurgeAsync();

        await server.MemoryPurgeAsync();
    }

    [Fact]
    public async Task get_allocator_stats()
    {
        await using var conn = Create(require: RedisFeatures.v4_0_0);

        var server = conn.GetServer(conn.GetEndPoints()[0]);

        var stats = server.MemoryAllocatorStats();
        string.IsNullOrWhiteSpace(stats).Should().BeFalse();

        stats = await server.MemoryAllocatorStatsAsync();
        string.IsNullOrWhiteSpace(stats).Should().BeFalse();
    }

    [Fact]
    public async Task get_stats()
    {
        await using var conn = Create(require: RedisFeatures.v4_0_0);

        var server = conn.GetServer(conn.GetEndPoints()[0]);
        var stats = server.MemoryStats();
        stats.Should().NotBeNull();
        stats.Resp2Type.Should().Be(ResultType.Array);

        var parsed = stats.ToDictionary();

        var alloc = parsed["total.allocated"];
        alloc.Resp2Type.Should().Be(ResultType.Integer);
        (alloc.AsInt64() > 0).Should().BeTrue();

        stats = await server.MemoryStatsAsync();
        stats.Should().NotBeNull();
        stats.Resp2Type.Should().Be(ResultType.Array);

        alloc = parsed["total.allocated"];
        alloc.Resp2Type.Should().Be(ResultType.Integer);
        (alloc.AsInt64() > 0).Should().BeTrue();
    }
}

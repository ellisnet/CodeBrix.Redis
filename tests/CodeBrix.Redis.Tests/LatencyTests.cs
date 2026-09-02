using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[Collection(NonParallelCollection.Name)]
public class LatencyTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task can_call_doctor()
    {
        await using var conn = Create();

        var server = conn.GetServer(conn.GetEndPoints()[0]);
        string? doctor = server.LatencyDoctor();
        doctor.Should().NotBeNull();
        doctor.Should().NotBe("");

        doctor = await server.LatencyDoctorAsync();
        doctor.Should().NotBeNull();
        doctor.Should().NotBe("");
    }

    [Fact]
    public async Task can_reset()
    {
        await using var conn = Create();

        var server = conn.GetServer(conn.GetEndPoints()[0]);
        _ = server.LatencyReset();
        var count = await server.LatencyResetAsync(["command"]);
        count.Should().Be(0);

        count = await server.LatencyResetAsync(["command", "fast-command"]);
        count.Should().Be(0);
    }

    [Fact]
    public async Task get_latest()
    {
        Skip.UnlessLongRunning();
        await using var conn = Create(allowAdmin: true);

        var server = conn.GetServer(conn.GetEndPoints()[0]);
        server.ConfigSet("latency-monitor-threshold", 50);
        server.LatencyReset();
        var arr = server.LatencyLatest();
        arr.Should().BeEmpty();

        var now = await server.TimeAsync();
        server.Execute("debug", "sleep", "0.5"); // cause something to be slow

        arr = await server.LatencyLatestAsync();
        var item = Assert.Single(arr);
        item.EventName.Should().Be("command");
        (item.DurationMilliseconds >= 400 && item.DurationMilliseconds <= 600).Should().BeTrue();
        item.MaxDurationMilliseconds.Should().Be(item.DurationMilliseconds);
        (item.Timestamp >= now.AddSeconds(-2) && item.Timestamp <= now.AddSeconds(2)).Should().BeTrue();
    }

    [Fact]
    public async Task get_history()
    {
        Skip.UnlessLongRunning();
        await using var conn = Create(allowAdmin: true);

        var server = conn.GetServer(conn.GetEndPoints()[0]);
        server.ConfigSet("latency-monitor-threshold", 50);
        server.LatencyReset();
        var arr = server.LatencyHistory("command");
        arr.Should().BeEmpty();

        var now = await server.TimeAsync();
        server.Execute("debug", "sleep", "0.5"); // cause something to be slow

        arr = await server.LatencyHistoryAsync("command");
        var item = Assert.Single(arr);
        (item.DurationMilliseconds >= 400 && item.DurationMilliseconds <= 600).Should().BeTrue();
        (item.Timestamp >= now.AddSeconds(-2) && item.Timestamp <= now.AddSeconds(2)).Should().BeTrue();
    }
}

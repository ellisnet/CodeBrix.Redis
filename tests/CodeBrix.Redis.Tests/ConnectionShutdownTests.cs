using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ConnectionShutdownTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task shutdown_raises_connection_failure_and_restore()
    {
        Assert.Skip("Unfriendly");

        await using var conn = Create(allowAdmin: true, allowSimulateConnectionFailure: true);

        int failed = 0, restored = 0;
        Stopwatch watch = Stopwatch.StartNew();
        conn.ConnectionFailed += (sender, args) =>
        {
            Log(watch.Elapsed + ": failed: " + EndPointCollection.ToString(args.EndPoint) + "/" + args.ConnectionType + ": " + args);
            Interlocked.Increment(ref failed);
        };
        conn.ConnectionRestored += (sender, args) =>
        {
            Log(watch.Elapsed + ": restored: " + EndPointCollection.ToString(args.EndPoint) + "/" + args.ConnectionType + ": " + args);
            Interlocked.Increment(ref restored);
        };
        var db = conn.GetDatabase();
        await db.PingAsync();
        Volatile.Read(ref failed).Should().Be(0);
        Volatile.Read(ref restored).Should().Be(0);
        await Task.Delay(1).ForAwait(); // To make compiler happy in Release

        conn.AllowConnect = false;
        var server = conn.GetServer(TestConfig.Current.PrimaryServer, TestConfig.Current.PrimaryPort);

        SetExpectedAmbientFailureCount(2);
        server.SimulateConnectionFailure(SimulatedFailureType.All);

        db.Ping(CommandFlags.FireAndForget);
        await Task.Delay(250).ForAwait();
        Volatile.Read(ref failed).Should().Be(2);
        Volatile.Read(ref restored).Should().Be(0);
        conn.AllowConnect = true;
        db.Ping(CommandFlags.FireAndForget);
        await Task.Delay(1500).ForAwait();
        Volatile.Read(ref failed).Should().Be(2);
        Volatile.Read(ref restored).Should().Be(2);
        watch.Stop();
    }
}

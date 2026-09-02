using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class RealWorldTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task why_does_this_not_work()
    {
        Log("first:");
        var config = ConfigurationOptions.Parse("localhost:6379,localhost:6380,name=Core (Q&A),tiebreaker=:RedisPrimary,abortConnect=False");
        config.EndPoints.Count.Should().Be(2);
        Log("Endpoint 0: {0} (AddressFamily: {1})", config.EndPoints[0], config.EndPoints[0].AddressFamily);
        Log("Endpoint 1: {0} (AddressFamily: {1})", config.EndPoints[1], config.EndPoints[1].AddressFamily);

        await using (var conn = ConnectionMultiplexer.Connect("localhost:6379,localhost:6380,name=Core (Q&A),tiebreaker=:RedisPrimary,abortConnect=False", Writer))
        {
            Log("");
            Log("pausing...");
            await Task.Delay(200, TestContext.Current.CancellationToken).ForAwait();
            Log("second:");

            bool result = conn.Configure(Writer);
            Log("Returned: {0}", result);
        }
    }
}

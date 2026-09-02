using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

[Collection(NonParallelCollection.Name)]
public class Issue2507(ITestOutputHelper output, SharedConnectionFixture? fixture = null) : TestBase(output, fixture)
{
    [Fact(Explicit = true)] // note this may show as Inconclusive, depending on the runner
    public async Task execute()
    {
        await using var conn = Create(shared: false);
        var db = conn.GetDatabase();
        var pubsub = conn.GetSubscriber();
        var queue = await pubsub.SubscribeAsync(RedisChannel.Literal("__redis__:invalidate"));
        await Task.Delay(100, TestContext.Current.CancellationToken);
        var connectionId = conn.GetConnectionId(conn.GetEndPoints().Single(), ConnectionType.Subscription);
        if (connectionId is null) Assert.Skip("Connection id not available");

        string baseKey = Me();
        RedisKey key1 = baseKey + "abc",
                 key2 = baseKey + "ghi",
                 key3 = baseKey + "mno";

        await db.StringSetAsync([new(key1, "def"), new(key2, "jkl"), new(key3, "pqr")]);
        // this is not supported, but: we want it to at least not fail
        await db.ExecuteAsync("CLIENT", "TRACKING", "on", "REDIRECT", connectionId!.Value, "BCAST");
        await db.KeyDeleteAsync([key1, key2, key3]);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        queue.Unsubscribe();
        queue.TryRead(out var message).Should().BeTrue("Queue 1 Read failed");
        message.Message.Should().Be((string?)key1);
        queue.TryRead(out message).Should().BeTrue("Queue 2 Read failed");
        message.Message.Should().Be((string?)key2);
        queue.TryRead(out message).Should().BeTrue("Queue 3 Read failed");
        message.Message.Should().Be((string?)key3);
        queue.TryRead(out message).Should().BeFalse("Queue 4 Read succeeded");
    }
}

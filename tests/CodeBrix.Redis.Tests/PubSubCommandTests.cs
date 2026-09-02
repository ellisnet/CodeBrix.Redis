using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class PubSubCommandTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    [Fact]
    public async Task subscriber_count()
    {
        await using var conn = Create();

        RedisChannel channel = RedisChannel.Literal(Me() + Guid.NewGuid());
        var server = conn.GetServer(conn.GetEndPoints()[0]);

        var channels = server.SubscriptionChannels(RedisChannel.Pattern(Me() + "*"));
        channels.Should().NotContain(channel);

        _ = server.SubscriptionPatternCount();
        var count = server.SubscriptionSubscriberCount(channel);
        count.Should().Be(0);
        conn.GetSubscriber().Subscribe(channel, (channel, value) => { });
        count = server.SubscriptionSubscriberCount(channel);
        count.Should().Be(1);

        channels = server.SubscriptionChannels(RedisChannel.Pattern(Me() + "*"));
        channels.Should().Contain(channel);
    }

    [Fact]
    public async Task subscriber_count_async()
    {
        await using var conn = Create();

        RedisChannel channel = RedisChannel.Literal(Me() + Guid.NewGuid());
        var server = conn.GetServer(conn.GetEndPoints()[0]);

        var channels = await server.SubscriptionChannelsAsync(RedisChannel.Pattern(Me() + "*")).WithTimeout(2000);
        channels.Should().NotContain(channel);

        _ = await server.SubscriptionPatternCountAsync().WithTimeout(2000);
        var count = await server.SubscriptionSubscriberCountAsync(channel).WithTimeout(2000);
        count.Should().Be(0);
        await conn.GetSubscriber().SubscribeAsync(channel, (channel, value) => { }).WithTimeout(2000);
        count = await server.SubscriptionSubscriberCountAsync(channel).WithTimeout(2000);
        count.Should().Be(1);

        channels = await server.SubscriptionChannelsAsync(RedisChannel.Pattern(Me() + "*")).WithTimeout(2000);
        channels.Should().Contain(channel);
    }
}
internal static class Util
{
    public static async Task WithTimeout(this Task task, int timeoutMs, [CallerMemberName] string? caller = null, [CallerLineNumber] int line = 0)
    {
        var cts = new CancellationTokenSource();
        if (task == await Task.WhenAny(task, Task.Delay(timeoutMs, cts.Token)).ForAwait())
        {
            cts.Cancel();
            await task.ForAwait();
        }
        else
        {
            throw new TimeoutException($"timeout from {caller} line {line}");
        }
    }
    public static async Task<T> WithTimeout<T>(this Task<T> task, int timeoutMs, [CallerMemberName] string? caller = null, [CallerLineNumber] int line = 0)
    {
        var cts = new CancellationTokenSource();
        if (task == await Task.WhenAny(task, Task.Delay(timeoutMs, cts.Token)).ForAwait())
        {
            cts.Cancel();
            return await task.ForAwait();
        }
        else
        {
            throw new TimeoutException($"timout from {caller} line {line}");
        }
    }
}

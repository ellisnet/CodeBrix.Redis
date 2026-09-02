using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

public class Issue2763Tests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task execute()
    {
        await using var conn = Create();
        var subscriber = conn.GetSubscriber();

        static void Handler(RedisChannel c, RedisValue v) { }

        const int COUNT = 1000;
        RedisChannel channel = RedisChannel.Literal("CHANNEL:TEST");

        List<Action> subscribes = new List<Action>(COUNT);
        for (int i = 0; i < COUNT; i++)
            subscribes.Add(() => subscriber.Subscribe(channel, Handler));
        Parallel.ForEach(subscribes, action => action());

        CountSubscriptionsForChannel(subscriber, channel).Should().Be(COUNT);

        List<Action> unsubscribes = new List<Action>(COUNT);
        for (int i = 0; i < COUNT; i++)
            unsubscribes.Add(() => subscriber.Unsubscribe(channel, Handler));
        Parallel.ForEach(unsubscribes, action => action());

        CountSubscriptionsForChannel(subscriber, channel).Should().Be(0);
    }

    private static int CountSubscriptionsForChannel(ISubscriber subscriber, RedisChannel channel)
    {
        ConnectionMultiplexer connMultiplexer = (ConnectionMultiplexer)subscriber.Multiplexer;
        connMultiplexer.GetSubscriberCounts(channel, out int handlers, out int _);
        return handlers;
    }
}

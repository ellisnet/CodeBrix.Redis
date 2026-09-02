using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Redis.Tests.RoundTripUnitTests;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class WriteFailureTeardownTests(ITestOutputHelper output) : TestBase(output)
{
    private sealed class ThrowingMessage(Exception toThrow, int db = -1, CommandFlags flags = CommandFlags.None, RedisCommand command = RedisCommand.PING)
        : Message(db, flags, command)
    {
        public override int ArgCount => 0;

        protected override void WriteImpl(in MessageWriter writer) => throw toThrow;
    }

    [Fact]
    public void write_to_propagates_write_impl_exception()
    {
        var inner = new InvalidOperationException("simulated write failure");
        var msg = new ThrowingMessage(inner);

        // The new behavior: WriteTo must rethrow so the bridge's outer catch can record a connection
        // failure. Passing null for physical is safe because WriteTo null-conditionals every member
        // access on it.
        using var ms = new MemoryStream();
        using var connection = PhysicalConnection.Dummy(ms);
        var thrown = Assert.Throws<InvalidOperationException>(() => msg.WriteTo(connection));
        thrown.Should().BeSameAs(inner);
    }

    [Fact]
    public void write_to_does_not_wrap_redis_command_exception()
    {
        // RedisCommandException is excluded from the catch filter (it carries its own meaning),
        // so it must surface unchanged from WriteImpl through WriteTo.
        var inner = new RedisCommandException("intentional");
        var msg = new ThrowingMessage(inner);
        using var ms = new MemoryStream();
        using var connection = PhysicalConnection.Dummy(ms);
        var thrown = Assert.Throws<RedisCommandException>(() => msg.WriteTo(connection));
        thrown.Should().BeSameAs(inner);
    }

    [Fact]
    public async Task write_failure_tears_down_physical_connection()
    {
        // We deliberately raise InternalError + ConnectionFailed events here, so don't fail
        // the test on ambient failures.
        SetExpectedAmbientFailureCount(-1);

        await using var conn = Create(shared: false, allowAdmin: true);

        int failedCount = 0;
        TaskCompletionSource<ConnectionFailureType> observedFailure = new();
        conn.ConnectionFailed += (_, e) =>
        {
            Interlocked.Increment(ref failedCount);
            observedFailure.TrySetResult(e.FailureType);
        };

        await conn.GetDatabase().PingAsync();

        var muxer = conn.UnderlyingMultiplexer;
        var server = muxer.GetServerSnapshot()[0];

        var boom = new InvalidOperationException("simulated WriteImpl failure");
        var throwingMsg = new ThrowingMessage(boom);
        muxer.CheckMessage(throwingMsg);

        var sendTask = muxer.ExecuteAsyncImpl(throwingMsg, ResultProcessor.ResponseTimer, state: null, server);

        // The throwing message should fault the awaiter (HandleWriteException calls SetExceptionAndComplete),
        // wrapping the inner exception in a RedisConnectionException with InternalFailure.
        var redisEx = await Assert.ThrowsAsync<RedisConnectionException>(async () => await sendTask);
        redisEx.FailureType.Should().Be(ConnectionFailureType.InternalFailure);

        // The new behavior: HandleWriteException calls RecordConnectionFailed on the physical
        // connection, which raises ConnectionFailed. Before this fix, no failure was raised
        // and the connection was left corrupt — the next response could match the wrong
        // in-flight message.
        await UntilConditionAsync(TimeSpan.FromSeconds(3), () => Volatile.Read(ref failedCount) > 0);
        (Volatile.Read(ref failedCount) > 0).Should().BeTrue("ConnectionFailed event did not fire after write failure");

        (await observedFailure.Task.WithTimeout(5000)).Should().Be(ConnectionFailureType.InternalFailure);
    }
}

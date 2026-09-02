using CodeBrix.Redis.Availability;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.RetryTests; //was previously: StackExchange.Redis.Tests.RetryTests;

/// <summary>
/// When a connection dies, one exception is built to describe the *connection*, and it used to be handed
/// verbatim to every message that was in flight. Sharing one exception instance across unrelated callers is
/// dubious in itself (<c>Exception.Data</c> is mutable), and it discards the per-message facts the retry
/// machinery needs: the command's retry category, and whether this particular message had actually been
/// written. Without them nothing at all is retryable after a connection failure, not even a plain
/// <c>GET</c>. <see cref="ExceptionFactory.PerMessage"/> is what splits them apart.
/// </summary>
public class ConnectionFaultDetailTests
{
    private static RedisConnectionException SharedConnectionFault()
    {
        // as built by PhysicalConnection.RecordConnectionFailed: describes the connection, knows nothing
        // about any individual message
        var ex = new RedisConnectionException(
            ConnectionFailureType.SocketClosed,
            CommandFlags.None,
            "SocketClosed on 127.0.0.1:6379/Interactive");
        ex.Data["Redis-Version"] = "1.2.3";
        ex.Data["Redis-Server"] = "127.0.0.1:6379";
        return ex;
    }

    private static Message Read() => Message.Create(0, CommandFlags.None, RedisCommand.GET, (RedisKey)"key");

    private static Message AccumulatingWrite() => Message.Create(0, CommandFlags.None, RedisCommand.INCR, (RedisKey)"key");

    // A message that was written before the socket died: the outcome is genuinely ambiguous, so the sent
    // status must survive as-is, but the command's category has to come through - otherwise the policy sees
    // "no category" and refuses to retry even a pure read.
    [Fact]
    public void sent_message_carries_category_and_sent_status()
    {
        var shared = SharedConnectionFault();
        var message = Read();
        message.SetRequestSent();

        var per = Assert.IsType<RedisConnectionException>(ExceptionFactory.PerMessage(shared, message));

        per.Should().NotBeSameAs(shared);
        per.FailureType.Should().Be(ConnectionFailureType.SocketClosed);
        per.CommandStatus.Should().Be(CommandStatus.Sent);
        (per.Flags & Message.MaskRetryCategory).Should().Be(CommandFlags.CommandRetryReadOnly);

        var ctx = new FaultContext(per);
        ctx.NotApplied.Should().BeFalse(); // it was on the wire; we cannot know whether the server ran it
        RetryPolicy.Default.CanRetry(in ctx).Should().NotBe(RetryResult.None); // ...but a read is safe

        // for contrast: the shared exception the message used to receive is retryable for nothing at all
        var sharedCtx = new FaultContext(shared);
        RetryPolicy.Default.CanRetry(in sharedCtx).Should().Be(RetryResult.None);
    }

    // Same situation, accumulating write: the category comes through and correctly *blocks* the retry, since
    // a replay could double-apply. The caller can still opt in by raising the cap.
    [Fact]
    public void sent_accumulating_write_remains_gated_by_category()
    {
        var message = AccumulatingWrite();
        message.SetRequestSent();

        var per = Assert.IsType<RedisConnectionException>(ExceptionFactory.PerMessage(SharedConnectionFault(), message));
        (per.Flags & Message.MaskRetryCategory).Should().Be(CommandFlags.CommandRetryWriteAccumulating);

        var ctx = new FaultContext(per);
        ctx.NotApplied.Should().BeFalse();
        RetryPolicy.Default.CanRetry(in ctx).Should().Be(RetryResult.None);

        RetryPolicy permissive = new RetryPolicy.Builder { MaxCommandRetryCategory = CommandFlags.CommandRetryWriteAccumulating };
        permissive.CanRetry(in ctx).Should().NotBe(RetryResult.None);
    }

    // A message that never left the client - still waiting to be written, or sitting in the backlog - is
    // *provably* unapplied, which is the one case where even an accumulating write can be safely re-issued.
    // That fact lives on the message, so sharing the connection's exception threw it away.
    [Theory]
    [InlineData(false)] // never handed to the bridge
    [InlineData(true)] // queued in the backlog awaiting a healthy connection
    public void unsent_message_is_known_not_applied(bool backlogged)
    {
        var message = AccumulatingWrite();
        if (backlogged) message.SetBacklogged();

        var per = Assert.IsType<RedisConnectionException>(ExceptionFactory.PerMessage(SharedConnectionFault(), message));

        per.CommandStatus.Should().Be(backlogged ? CommandStatus.WaitingInBacklog : CommandStatus.WaitingToBeSent);

        var ctx = new FaultContext(per);
        ctx.NotApplied.Should().BeTrue();
        // accumulating, i.e. beyond the default cap - but nothing was applied, so there is nothing to repeat
        RetryPolicy.Default.CanRetry(in ctx).Should().NotBe(RetryResult.None);
    }

    // The connection-level diagnostics are the useful part of these exceptions, so they have to come across;
    // but the dictionaries must be independent, or one caller's annotations show up on another's exception.
    [Fact]
    public void shared_diagnostics_are_copied_but_not_shared()
    {
        var shared = SharedConnectionFault();
        var first = ExceptionFactory.PerMessage(shared, Read());
        var second = ExceptionFactory.PerMessage(shared, AccumulatingWrite());

        second.Should().NotBeSameAs(first);
        first.Message.Should().Be(shared.Message);
        first.Data["Redis-Version"].Should().Be("1.2.3");
        second.Data["Redis-Version"].Should().Be("1.2.3");

        first.Data["mine"] = "only-mine";
        second.Data.Contains("mine").Should().BeFalse();
        shared.Data.Contains("mine").Should().BeFalse();
    }

    // The per-message status is recorded in the diagnostic data too, so a user reading the exception's Data
    // sees this message's status rather than whatever the connection-level exception happened to say.
    [Fact]
    public void sent_status_is_recorded_in_diagnostic_data()
    {
        var shared = SharedConnectionFault();
        shared.Data["request-sent-status"] = CommandStatus.Unknown;

        var message = Read();
        message.SetRequestSent();
        var per = ExceptionFactory.PerMessage(shared, message);

        per.Data["request-sent-status"].Should().Be(CommandStatus.Sent);
    }

    // Only the shared connection-failure shape needs splitting; anything else already describes a single
    // operation, and an exception that already matches the message is passed straight through (no needless
    // allocation on a teardown that may be failing thousands of messages).
    [Fact]
    public void unrelated_or_already_matching_exceptions_are_passed_through()
    {
        var message = Read();
        message.SetRequestSent();

        var serverFault = new RedisServerException(RedisErrorKind.Loading, message.Flags, "LOADING");
        ExceptionFactory.PerMessage(serverFault, message).Should().BeSameAs(serverFault);

        var alreadySpecific = new RedisConnectionException(
            ConnectionFailureType.SocketClosed,
            message.Flags,
            "already describes this message",
            innerException: null,
            commandStatus: CommandStatus.Sent);
        ExceptionFactory.PerMessage(alreadySpecific, message).Should().BeSameAs(alreadySpecific);
    }
}

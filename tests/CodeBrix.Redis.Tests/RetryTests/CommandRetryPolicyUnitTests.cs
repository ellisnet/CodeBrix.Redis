using System.Threading;
using CodeBrix.Redis.Availability;
using CodeBrix.Redis.Interfaces;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.RetryTests; //was previously: StackExchange.Redis.Tests.RetryTests;

public class CommandRetryPolicyUnitTests
{
    // --- RetryPolicy.CanRetry: spoofed fault scenarios -------------------------------

    // Builds a FaultContext for a spoofed server error of the given kind, carrying the given
    // command-flags, and asks the policy whether it may be retried.
    private static RetryResult CanRetry(RedisErrorKind kind, CommandFlags flags, RetryPolicy? policy = null)
    {
        // the exception carries both the Kind and the command-flags; FaultContext reads them back
        var fault = new FaultContext(new RedisServerException(kind, flags, kind.ToString()));
        return (policy ?? RetryPolicy.Default).CanRetry(in fault);
    }

    // As above, but for an *ambiguous* fault: a timeout on a request we know was sent. We have no idea
    // whether the server applied it, so this is the case the retry-category exists to price.
    private static RetryResult CanRetryAmbiguous(CommandFlags flags, RetryPolicy? policy = null)
    {
        var fault = new FaultContext(new RedisTimeoutException(flags, "timeout", CommandStatus.Sent));
        return (policy ?? RetryPolicy.Default).CanRetry(in fault);
    }

    // The command's retry-category is checked against the policy's max category: the default max is
    // CommandRetryWriteLastWins, so anything at-or-below that is in-range, and anything with more
    // side-effects is not. Using an ambiguous (timeout-after-send) fault, since that is where the
    // category actually bites - see can_retry_not_applied_bypasses_category for the other half.
    [Theory]
    [InlineData(CommandFlags.CommandRetryAlways, true)]
    [InlineData(CommandFlags.CommandRetryConnection, true)]
    [InlineData(CommandFlags.CommandRetryReadOnly, true)]
    [InlineData(CommandFlags.CommandRetryWriteChecked, true)]
    [InlineData(CommandFlags.CommandRetryWriteLastWins, true)] // == default max
    [InlineData(CommandFlags.CommandRetryWriteAccumulating, false)] // beyond default max
    [InlineData(CommandFlags.CommandRetryServerAdmin, false)]
    [InlineData(CommandFlags.CommandRetryNever, false)]
    [InlineData(CommandFlags.None, false)] // unspecified => assume the worst => not retried
    public void can_retry_category_versus_default_max(CommandFlags category, bool expectRetry)
    {
        var result = CanRetryAmbiguous(category);
        (result != RetryResult.None).Should().Be(expectRetry);
    }

    // When the fault proves the operation never took effect (here: the server was still LOADING, so it
    // rejected the command outright), a replay is a first attempt rather than a repeat - it cannot
    // double-apply anything, so the side-effect category is irrelevant and the cap is bypassed. An
    // explicit CommandRetryNever is still an absolute veto, as is an unspecified category.
    [Theory]
    [InlineData(CommandFlags.CommandRetryWriteAccumulating, true)] // beyond the default cap, but safe here
    [InlineData(CommandFlags.CommandRetryServerAdmin, true)]
    [InlineData(CommandFlags.CommandRetryNever, false)] // never means never
    [InlineData(CommandFlags.None, false)] // we don't know what it is; don't guess
    public void can_retry_not_applied_bypasses_category(CommandFlags category, bool expectRetry)
    {
        // sanity: the same category against an ambiguous fault of the same "retryability" is refused
        if (expectRetry) CanRetryAmbiguous(category).Should().Be(RetryResult.None);

        var result = CanRetry(RedisErrorKind.Loading, category);
        (result != RetryResult.None).Should().Be(expectRetry);
    }

    // The other source of certainty: the client knows it never wrote the message. Same conclusion, even
    // though the fault itself (a connection failure) is otherwise ambiguous.
    [Theory]
    [InlineData(CommandStatus.WaitingToBeSent, true)]
    [InlineData(CommandStatus.WaitingInBacklog, true)]
    [InlineData(CommandStatus.Sent, false)] // may or may not have been applied
    [InlineData(CommandStatus.Unknown, false)]
    public void can_retry_unsent_message_bypasses_category(CommandStatus status, bool expectRetry)
    {
        var fault = new FaultContext(new RedisConnectionException(
            ConnectionFailureType.SocketFailure,
            CommandFlags.CommandRetryWriteAccumulating, // beyond the default cap
            "boom",
            innerException: null,
            commandStatus: status));

        fault.NotApplied.Should().Be(expectRetry);
        (RetryPolicy.Default.CanRetry(in fault) != RetryResult.None).Should().Be(expectRetry);
    }

    // With an in-range category (== default max), the outcome is decided purely by whether the error
    // is transient: LOADING is worth retrying, WRONGTYPE is an application error that will not fix itself.
    [Theory]
    [InlineData(RedisErrorKind.Loading, true)] // still loading the dataset - transient
    [InlineData(RedisErrorKind.ClusterDown, true)] // slot temporarily unserved - transient
    [InlineData(RedisErrorKind.WrongType, false)] // wrong data type - application error
    [InlineData(RedisErrorKind.NoPermission, false)] // ACL - application error
    public void can_retry_error_kind_gates_retry_when_in_range(RedisErrorKind kind, bool expectRetry)
    {
        var result = CanRetry(kind, CommandFlags.CommandRetryWriteLastWins);
        (result != RetryResult.None).Should().Be(expectRetry);
    }

    // "never" and "always" adjust only the category range - they do not override the error-kind check:
    // an "always" command still won't retry an application error, and a "never" command won't retry even
    // a transient one.
    [Theory]
    [InlineData(CommandFlags.CommandRetryAlways, RedisErrorKind.Loading, true)]
    [InlineData(CommandFlags.CommandRetryAlways, RedisErrorKind.WrongType, false)]
    [InlineData(CommandFlags.CommandRetryNever, RedisErrorKind.Loading, false)]
    [InlineData(CommandFlags.CommandRetryNever, RedisErrorKind.WrongType, false)]
    public void can_retry_never_and_always_affect_range_not_error_kind(CommandFlags category, RedisErrorKind kind, bool expectRetry)
    {
        var result = CanRetry(kind, category);
        (result != RetryResult.None).Should().Be(expectRetry);
    }

    // When a retry is permitted, it normally offers both the same server and a failover server; but a
    // "server specific" (sticky) command must not move endpoints, so only the same-server option remains.
    [Theory]
    [InlineData(CommandFlags.None, RetryResult.SameServer | RetryResult.FailoverServer)]
    [InlineData(Message.CommandServerSpecific, RetryResult.SameServer)]
    public void can_retry_server_specific_restricts_to_same_server(CommandFlags extra, RetryResult expected)
    {
        // in-range category (== default max) + transient error => a retry is offered; the sticky flag
        // only changes *where* the retry may go, not *whether* it happens.
        var result = CanRetry(RedisErrorKind.Loading, CommandFlags.CommandRetryWriteLastWins | extra);
        result.Should().Be(expected);
    }

    // The sticky (server-specific) flag lives outside the retry-category region, so it is masked off
    // before the category-vs-max comparison and must not change the range verdict (retry-at-all vs none)
    // - it only affects the same/failover choice, covered above.
    [Theory]
    [InlineData(CommandFlags.CommandRetryReadOnly, true)] // in range
    [InlineData(CommandFlags.CommandRetryServerAdmin, false)] // beyond default max
    public void can_retry_server_specific_does_not_affect_range(CommandFlags category, bool expectRetry)
    {
        var withoutFlag = CanRetryAmbiguous(category);
        var withFlag = CanRetryAmbiguous(category | Message.CommandServerSpecific);

        (withoutFlag != RetryResult.None).Should().Be(expectRetry);
        (withFlag != RetryResult.None).Should().Be(expectRetry);
    }

    // --- RetryDatabase.CanRetry: attempt accounting ----------------------------------

    // With max-attempts-before-failover pinned equal to max-attempts, the failover path is disabled, so
    // this exercises pure same-server attempt counting. A transient LOADING fault on an in-range command
    // means the policy would allow a retry; the only gate is the attempt counter: with MaxAttempts=3,
    // attempts 1 and 2 may retry, attempt 3 is exhausted. Because we never fail over, the out "delay" is
    // never cancellable (that is how "don't wait for failover" is expressed) and the ref "failover" is
    // left untouched.
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void retry_database_can_retry_max_attempts_no_failover(int attempt, bool expected)
    {
        RetryPolicy policy = new RetryPolicy.Builder { MaxAttempts = 3, MaxAttemptsBeforeFailover = 3 };
        var controller = new RetryController(policy, DatabaseFeatureFlags.None); // CanRetry never touches any database

        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var failover = token;

        var fault = new RedisServerException(RedisErrorKind.Loading, CommandFlags.CommandRetryWriteLastWins, "LOADING");
        var result = controller.CanRetry(attempt, fault, ref failover, out var delay);

        result.Should().Be(expected);
        delay.CanBeCanceled.Should().BeFalse(); // never waiting for a failover
        failover.Should().Be(token); // ref failover untouched
    }

    // MaxAttempts=4 with failover enabled after 2 attempts. A single "failover" token is threaded through
    // the sequence to observe the state machine: attempts 1..3 return true, attempt 4 is exhausted. The
    // interesting step is attempt 2 (== MaxAttemptsBeforeFailover): it still returns true, but now hands the
    // failover token back as "delay" and clears the ref (we fail over only once); attempt 3 therefore sees
    // no failover token and drops back to a plain same-server retry.
    [Fact]
    public void retry_database_can_retry_failover_at_threshold()
    {
        RetryPolicy policy = new RetryPolicy.Builder { MaxAttempts = 4, MaxAttemptsBeforeFailover = 2 };
        // failover is only armed when the inner database advertises the feature; supply it explicitly
        var controller = new RetryController(policy, DatabaseFeatureFlags.Failover);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var failover = token;
        var fault = new RedisServerException(RedisErrorKind.Loading, CommandFlags.CommandRetryWriteLastWins, "LOADING");

        // attempt 1: plain same-server retry; failover token not yet consumed
        controller.CanRetry(1, fault, ref failover, out var delay).Should().BeTrue();
        delay.CanBeCanceled.Should().BeFalse();
        failover.Should().Be(token);

        // attempt 2 (== MaxAttemptsBeforeFailover): still a retry, but now fail over - "delay" becomes the
        // failover token and the ref is cleared to None so it only fires once
        controller.CanRetry(2, fault, ref failover, out delay).Should().BeTrue();
        delay.Should().Be(token);
        delay.CanBeCanceled.Should().BeTrue();
        failover.Should().Be(CancellationToken.None);

        // attempt 3: failover already spent (ref is None) -> back to a same-server retry
        controller.CanRetry(3, fault, ref failover, out delay).Should().BeTrue();
        delay.CanBeCanceled.Should().BeFalse();
        failover.Should().Be(CancellationToken.None);

        // attempt 4: no retries left
        controller.CanRetry(4, fault, ref failover, out delay).Should().BeFalse();
        delay.CanBeCanceled.Should().BeFalse();
    }

    // As above, but with the sticky (server-specific) flag set: the policy now permits only same-server
    // retries, so there is no failover option at the threshold. Current behaviour: CanRetry returns *false*
    // at attempt 2 - the command gives up rather than continuing on the same server - because the threshold
    // branch (attempt == MaxAttemptsBeforeFailover) requires FailoverServer permission and does not fall
    // back to a same-server retry. It also consumes the failover token as a side-effect of that branch.
    [Fact]
    public void retry_database_can_retry_server_specific_cannot_failover()
    {
        RetryPolicy policy = new RetryPolicy.Builder { MaxAttempts = 4, MaxAttemptsBeforeFailover = 2 };
        var controller = new RetryController(policy, DatabaseFeatureFlags.Failover);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var failover = token;
        const CommandFlags flags = CommandFlags.CommandRetryWriteLastWins | Message.CommandServerSpecific;
        var fault = new RedisServerException(RedisErrorKind.Loading, flags, "LOADING");

        // attempt 1: same-server retry; failover token untouched
        controller.CanRetry(1, fault, ref failover, out var delay).Should().BeTrue();
        delay.CanBeCanceled.Should().BeFalse();
        failover.Should().Be(token);

        // attempt 2 (== MaxAttemptsBeforeFailover): sticky forbids failover -> gives up (false), even though
        // attempts remain; the failover token is still consumed to None as a side-effect of the branch
        controller.CanRetry(2, fault, ref failover, out delay).Should().BeFalse();
        failover.Should().Be(CancellationToken.None);
    }
}

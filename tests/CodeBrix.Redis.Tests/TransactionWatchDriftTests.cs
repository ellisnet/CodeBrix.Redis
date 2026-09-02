using System.Net;
using System.Threading.Tasks;
using CodeBrix.Redis.Respite.Messages;
using CodeBrix.Redis.TestServer;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

/// <summary>
/// Covers the "WATCH drift" outcome of a conditional transaction: every condition was satisfied, so
/// <c>MULTI</c>/<c>EXEC</c> really was issued, but a watched key changed underneath us and the server
/// answered <c>EXEC</c> with a null array. This is distinct from an *elective* abort (a condition that
/// failed, where no <c>EXEC</c> is sent at all) and, unlike that case, it can only be produced by a
/// concurrent write - so it needs the in-process server to drive it deterministically.
/// </summary>
[RunPerProtocol]
public class TransactionWatchDriftTests(ITestOutputHelper log) : TestBase(log)
{
    // A null array is not an empty array: EXEC answering *-1 (RESP2) / _ (RESP3) means "watch failed",
    // whereas *0 means "a transaction of zero commands committed". The managed server used to collapse
    // the former into the latter, which made the whole drift path untestable (and, client-side, it
    // surfaced as a protocol failure instead).
    [Fact]
    public void null_array_is_distinct_from_empty_array()
    {
        var nullArray = TypedRedisValue.NullArray(RespPrefix.Array);
        nullArray.IsNullArray.Should().BeTrue();
        nullArray.IsNullValueOrArray.Should().BeTrue();
        nullArray.Span.IsEmpty.Should().BeTrue();

        var emptyArray = TypedRedisValue.EmptyArray(RespPrefix.Array);
        emptyArray.IsNullArray.Should().BeFalse();
        emptyArray.IsNullValueOrArray.Should().BeFalse();
        emptyArray.Span.IsEmpty.Should().BeTrue();
    }

    // The headline case: the condition holds, EXEC is issued, and the server rejects it because the
    // watched key moved. Execute reports false (nothing was applied) and - the part that regressed -
    // every queued operation's task must reach a terminal state (cancelled), not hang forever.
    [Fact]
    public async Task watch_drift_aborts_and_cancels_queued_operations()
    {
        using var server = new WatchDriftServer(Output);
        await using var conn = await server.ConnectAsync(log: Writer);

        var db = conn.GetDatabase();
        RedisKey key = "drift:cancel";
        (await db.StringSetAsync(key, "seed")).Should().BeTrue();

        server.DriftKey = key;
        server.DriftOps = 1; // the next EXEC observes a concurrent write to the watched key

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(Condition.StringEqual(key, "seed"));
        var setTask = tran.StringSetAsync(key, "committed");
        var incrTask = tran.StringIncrementAsync("drift:counter");

        (await tran.ExecuteAsync()).Should().BeFalse(); // EXEC returned a null array
        cond.WasSatisfied.Should().BeTrue(); // the *condition* held; the server-side WATCH is what killed it
        tran.WasWatchConflict.Should().BeTrue(); // ...and this is how a caller tells the two apart
        server.ExecOpsReceived.Should().Be(1);

        // both per-operation tasks must complete (as cancelled); before the fix they sat forever in
        // WaitingForActivation, so assert with a timeout rather than awaiting them directly
        await AssertCancelledAsync(setTask);
        await AssertCancelledAsync(incrTask);

        (await db.StringGetAsync(key)).Should().Be("seed"); // nothing was applied
        (await db.KeyExistsAsync("drift:counter")).Should().BeFalse();
    }

    // Same shape, but confirming the *elective* abort still behaves: the condition fails, no EXEC is
    // ever issued, and the queued operations are cancelled. This is the path that already worked; it is
    // here so the two outcomes are pinned side by side (they are indistinguishable from Execute's bool
    // alone - WasWatchConflict, or inspecting the ConditionResults, is what separates them).
    [Fact]
    public async Task failed_condition_aborts_electively_without_exec()
    {
        using var server = new WatchDriftServer(Output);
        await using var conn = await server.ConnectAsync(log: Writer);

        var db = conn.GetDatabase();
        RedisKey key = "drift:elective";
        (await db.StringSetAsync(key, "seed")).Should().BeTrue();

        var tran = db.CreateTransaction();
        tran.WasWatchConflict.Should().BeFalse(); // false before execution, too

        var cond = tran.AddCondition(Condition.StringEqual(key, "different"));
        var setTask = tran.StringSetAsync(key, "committed");

        (await tran.ExecuteAsync()).Should().BeFalse();
        cond.WasSatisfied.Should().BeFalse(); // this is what distinguishes an elective abort from drift
        tran.WasWatchConflict.Should().BeFalse(); // we chose not to issue an EXEC; nobody raced us
        server.ExecOpsReceived.Should().Be(0); // never even asked

        await AssertCancelledAsync(setTask);
        (await db.StringGetAsync(key)).Should().Be("seed");
    }

    // A transaction with conditions but no operations: drift still aborts it, and there are no
    // per-operation tasks to cancel. Guards the zero-length inner-operations edge in the processor.
    [Fact]
    public async Task watch_drift_condition_only_transaction_aborts()
    {
        using var server = new WatchDriftServer(Output);
        await using var conn = await server.ConnectAsync(log: Writer);

        var db = conn.GetDatabase();
        RedisKey key = "drift:condonly";
        (await db.StringSetAsync(key, "seed")).Should().BeTrue();

        server.DriftKey = key;
        server.DriftOps = 1;

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(Condition.StringEqual(key, "seed"));

        (await tran.ExecuteAsync()).Should().BeFalse();
        cond.WasSatisfied.Should().BeTrue();
        tran.WasWatchConflict.Should().BeTrue();
        server.ExecOpsReceived.Should().Be(1);
    }

    // A transaction that commits cleanly must not report a conflict.
    [Fact]
    public async Task satisfied_condition_commits_without_conflict()
    {
        using var server = new WatchDriftServer(Output);
        await using var conn = await server.ConnectAsync(log: Writer);

        var db = conn.GetDatabase();
        RedisKey key = "drift:clean";
        (await db.StringSetAsync(key, "seed")).Should().BeTrue();

        var tran = db.CreateTransaction();
        var cond = tran.AddCondition(Condition.StringEqual(key, "seed"));
        var setTask = tran.StringSetAsync(key, "committed");

        (await tran.ExecuteAsync()).Should().BeTrue();
        cond.WasSatisfied.Should().BeTrue();
        tran.WasWatchConflict.Should().BeFalse();
        (await setTask).Should().BeTrue();
    }

    private async Task AssertCancelledAsync(Task task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(5000, TestContext.Current.CancellationToken));
        if (completed != task)
        {
            Log($"task did not complete; status: {task.Status}");
            Assert.Fail($"queued operation never completed (status: {task.Status})");
        }

        await Assert.ThrowsAnyAsync<System.OperationCanceledException>(async () => await task);
    }

    // An in-process server that, for the next DriftOps EXEC operations, simulates a concurrent write to
    // DriftKey immediately before the EXEC is processed. Touch is exactly what a real write from another
    // connection would do, so the transaction is doomed by the server's own WATCH bookkeeping and EXEC
    // replies with a null array - no special-casing of the reply itself.
    //
    // Driving this from a genuinely separate connection is not practical: SE.Redis does not issue the WATCH
    // when AddCondition is called, it issues WATCH, the condition reads, MULTI, the queued commands and
    // EXEC as one dispatch. So the window an interloper has to squeeze into is the gap between the
    // condition reads and the EXEC landing, within a single flush - which is the point of the feature, but
    // makes it useless as a test lever. Injecting the Touch server-side reproduces the same state exactly.
    private sealed class WatchDriftServer(ITestOutputHelper? log, EndPoint? endpoint = null) : InProcessTestServer(log, endpoint)
    {
        public int ExecOpsReceived { get; private set; }

        public int DriftOps { get; set; }

        public RedisKey DriftKey { get; set; }

        protected override TypedRedisValue Exec(RedisClient client, in RedisRequest request)
        {
            ExecOpsReceived++;

            if (DriftOps > 0)
            {
                DriftOps--;
                client.Touch(client.Database, DriftKey);
            }

            return base.Exec(client, in request);
        }
    }
}

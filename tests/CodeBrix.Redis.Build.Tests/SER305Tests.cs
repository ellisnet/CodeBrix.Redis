using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CodeBrix.Redis.Build.Tests; //was previously: StackExchange.Redis.Build.Tests;

/// <summary>
/// Waiting for a command queued on a transaction or batch, at the point of queueing - which never completes.
/// </summary>
/// <remarks>
/// The only error-severity rule in the set, so the negative cases matter more here than anywhere else: a false
/// positive is a broken build on code that works. <see cref="SER306"/> covers the fire-and-forget case, which
/// is the one shape that survives the wait.
/// </remarks>
public class SER305Tests : Verifier<QueuedResultAnalyzer>
{
    [Fact]
    public Task awaited_transaction_command_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                var value = {|#0:await {|#1:tran.StringGetAsync(key)|}|};
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER305", DiagnosticSeverity.Error).WithLocation(0).WithLocation(1).WithArguments("StringGetAsync", "a transaction"));

    [Fact]
    public Task awaited_batch_command_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var batch = db.CreateBatch();
                {|#0:await {|#1:batch.StringSetAsync(key, "value")|}|};
                batch.Execute();
            }
        }
        """,
        Diagnostic("SER305", DiagnosticSeverity.Error).WithLocation(0).WithLocation(1).WithArguments("StringSetAsync", "a batch"));

    [Fact]
    public Task blocking_on_result_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public void M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                var value = {|#0:{|#1:tran.StringGetAsync(key)|}.Result|};
                tran.Execute();
            }
        }
        """,
        Diagnostic("SER305", DiagnosticSeverity.Error).WithLocation(0).WithLocation(1).WithArguments("StringGetAsync", "a transaction"));

    [Fact]
    public Task blocking_on_wait_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public void M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                {|#0:{|#1:tran.StringSetAsync(key, "value")|}.Wait()|};
                tran.Execute();
            }
        }
        """,
        Diagnostic("SER305", DiagnosticSeverity.Error).WithLocation(0).WithLocation(1).WithArguments("StringSetAsync", "a transaction"));

    [Fact]
    public Task blocking_on_get_awaiter_get_result_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public void M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                var value = {|#0:{|#1:tran.StringGetAsync(key)|}.GetAwaiter().GetResult()|};
                tran.Execute();
            }
        }
        """,
        Diagnostic("SER305", DiagnosticSeverity.Error).WithLocation(0).WithLocation(1).WithArguments("StringGetAsync", "a transaction"));

    /// <summary>
    /// The trap the rule is written around: this <c>ExecuteAsync</c> is <c>IDatabaseAsync</c>'s raw-command
    /// escape hatch, which queues like anything else - not <c>ITransaction</c>'s terminator of the same name.
    /// </summary>
    [Fact]
    public Task awaited_raw_command_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db)
            {
                var tran = db.CreateTransaction();
                var result = {|#0:await {|#1:tran.ExecuteAsync("PING")|}|};
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER305", DiagnosticSeverity.Error).WithLocation(0).WithLocation(1).WithArguments("ExecuteAsync", "a transaction"));

    [Fact]
    public Task awaited_with_unrelated_flags_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                var value = {|#0:await {|#1:tran.StringGetAsync(key, CommandFlags.DemandMaster)|}|};
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER305", DiagnosticSeverity.Error).WithLocation(0).WithLocation(1).WithArguments("StringGetAsync", "a transaction"));

    [Fact]
    public Task awaited_on_transaction_async_interface_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(ITransactionAsync tran, RedisKey key)
            {
                var value = {|#0:await {|#1:tran.StringGetAsync(key)|}|};
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER305", DiagnosticSeverity.Error).WithLocation(0).WithLocation(1).WithArguments("StringGetAsync", "a transaction"));

    // ---- negative cases: everything below must stay silent ----

    /// <summary>
    /// A terminator reached through an interface that derives from <c>ITransaction</c> still resolves to
    /// <c>ITransaction</c>'s member, so it is recognised for free.
    /// </summary>
    /// <remarks>
    /// The case this *cannot* reach from here is a decorator **class** implementing <c>ITransaction</c>, where
    /// the resolved member belongs to the class rather than the interface - which is a real false positive, and
    /// is what <c>KnownSymbols.IsTerminator</c>'s implementation walk exists for. There is no golden test for it
    /// because no public type in the library implements <c>ITransaction</c> (they are all internal) and a test
    /// source cannot declare one: an abstract class may not leave interface members unimplemented (CS0535), so
    /// it would have to spell out the whole of <c>IDatabaseAsync</c>. It is covered instead by this repo's own
    /// build, where <c>KeyPrefixedTransactionTests.ExecuteAsync</c> failing to compile is exactly that
    /// regression - which is how the bug was found in the first place.
    /// </remarks>
    [Fact]
    public Task awaited_terminator_on_derived_interface_is_clean() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        interface IMyTransaction : ITransaction { }
        class C
        {
            public async Task M(IMyTransaction tran, RedisKey key)
            {
                _ = tran.StringSetAsync(key, "value");
                var committed = await tran.ExecuteAsync();
            }
        }
        """);

    /// <summary>...but a queued command on that same interface is still a queued command.</summary>
    [Fact]
    public Task awaited_command_on_derived_interface_is_flagged() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        interface IMyTransaction : ITransaction { }
        class C
        {
            public async Task M(IMyTransaction tran, RedisKey key)
            {
                var value = {|#0:await {|#1:tran.StringGetAsync(key)|}|};
                await tran.ExecuteAsync();
            }
        }
        """,
        Diagnostic("SER305", DiagnosticSeverity.Error).WithLocation(0).WithLocation(1).WithArguments("StringGetAsync", "a transaction"));

    /// <summary>The shape the rule is steering people towards, which must never be flagged.</summary>
    [Fact]
    public Task captured_and_awaited_after_execute_is_clean() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                var pending = tran.StringGetAsync(key);
                await tran.ExecuteAsync();
                var value = await pending;
            }
        }
        """);

    /// <summary>
    /// The same capture, awaited *before* Execute - genuinely broken, but only knowable by ordering, which is
    /// out of scope on purpose. Pinned so that "no diagnostic" here is a decision rather than an oversight.
    /// </summary>
    [Fact]
    public Task captured_and_awaited_before_execute_is_not_reported() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                var pending = tran.StringGetAsync(key);
                var value = await pending;
                await tran.ExecuteAsync();
            }
        }
        """);

    [Fact]
    public Task awaited_terminator_is_clean() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                _ = tran.StringSetAsync(key, "value");
                var committed = await tran.ExecuteAsync();
            }
        }
        """);

    [Fact]
    public Task awaited_on_database_is_clean() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var value = await db.StringGetAsync(key);
                await db.StringSetAsync(key, "value");
            }
        }
        """);

    /// <summary>
    /// A transaction reached through <c>IDatabaseAsync</c> - which a helper method taking one cannot tell from
    /// a plain database, and neither can we.
    /// </summary>
    [Fact]
    public Task awaited_through_database_async_parameter_is_clean() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabaseAsync db, RedisKey key)
            {
                var value = await db.StringGetAsync(key);
            }
        }
        """);

    /// <summary>
    /// Non-constant flags may or may not carry FireAndForget at run-time, and an error must not guess: this is
    /// the wrapper-method shape, where the caller's flags are passed straight through.
    /// </summary>
    [Fact]
    public Task awaited_with_non_constant_flags_is_clean() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key, CommandFlags flags)
            {
                var tran = db.CreateTransaction();
                var value = await tran.StringGetAsync(key, flags);
                await tran.ExecuteAsync();
            }
        }
        """);

    [Fact]
    public Task discarded_queued_command_is_clean() => VerifyAsync(
        """
        using CodeBrix.Redis;
        using System.Threading.Tasks;
        class C
        {
            public async Task M(IDatabase db, RedisKey key)
            {
                var tran = db.CreateTransaction();
                _ = tran.StringSetAsync(key, "value");
                tran.AddCondition(Condition.KeyExists(key));
                await tran.ExecuteAsync();
            }
        }
        """);
}

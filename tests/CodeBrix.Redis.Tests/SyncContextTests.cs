using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class SyncContextTests(ITestOutputHelper testOutput) : TestBase(testOutput)
{
    /* Note A (referenced below)
     *
     * When sync-context is *enabled*, we don't validate OpCount > 0 - this is because *with the additional checks*,
     * it can genuinely happen that by the time we actually await it, it has completed - which results in a brittle test.
     */
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task detect_sync_context_unsafe(bool continueOnCapturedContext)
    {
        using var ctx = new MySyncContext(Writer);
        ctx.OpCount.Should().Be(0);
        await Task.Delay(100, TestContext.Current.CancellationToken).ConfigureAwait(continueOnCapturedContext);

        AssertState(continueOnCapturedContext, ctx);
    }

    private void AssertState(bool continueOnCapturedContext, MySyncContext ctx)
    {
        Log($"Context in AssertState: {ctx}");
        if (continueOnCapturedContext)
        {
            ctx.IsCurrent.Should().BeTrue(nameof(ctx.IsCurrent));
            // see note A re OpCount
        }
        else
        {
            // no guarantees on sync-context still being current; depends on sync vs async
            ctx.OpCount.Should().Be(0);
        }
    }

    [Fact]
    public async Task sync_ping()
    {
        using var ctx = new MySyncContext(Writer);
        await using var conn = Create();
        ctx.OpCount.Should().Be(0);
        var db = conn.GetDatabase();
        db.Ping();
        ctx.OpCount.Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task async_ping(bool continueOnCapturedContext)
    {
        using var ctx = new MySyncContext(Writer);
        await using var conn = Create();
        ctx.OpCount.Should().Be(0);
        var db = conn.GetDatabase();
        Log($"Context before await: {ctx}");
        await db.PingAsync().ConfigureAwait(continueOnCapturedContext);

        AssertState(continueOnCapturedContext, ctx);
    }

    [Fact]
    public async Task sync_configure()
    {
        using var ctx = new MySyncContext(Writer);
        await using var conn = Create();
        ctx.OpCount.Should().Be(0);
        // ReSharper disable once MethodHasAsyncOverload - very deliberate
        conn.Configure().Should().BeTrue();
        ctx.OpCount.Should().Be(0);
    }

    [Theory]
    [InlineData(true)] // fail: Expected: Not RanToCompletion, Actual: RanToCompletion
    [InlineData(false)] // pass
    public async Task async_configure(bool continueOnCapturedContext)
    {
        using var ctx = new MySyncContext(Writer);
        await using var conn = Create();

        Log($"Context initial: {ctx}");
        await Task.Delay(500, TestContext.Current.CancellationToken);
        await conn.GetDatabase().PingAsync(); // ensure we're all ready
        ctx.Reset();
        Log($"Context before: {ctx}");

        ctx.OpCount.Should().Be(0);
        (await conn.ConfigureAsync(Writer).ConfigureAwait(continueOnCapturedContext)).Should().BeTrue("config ran");

        AssertState(continueOnCapturedContext, ctx);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task connect_async(bool continueOnCapturedContext)
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        using var ctx = new MySyncContext(Writer);
        var config = GetConfiguration(); // not ideal, but sufficient
        await ConnectionMultiplexer.ConnectAsync(config, Writer).ConfigureAwait(continueOnCapturedContext);

        AssertState(continueOnCapturedContext, ctx);
    }

    public sealed class MySyncContext : SynchronizationContext, IDisposable
    {
        private readonly SynchronizationContext? _previousContext;
        private readonly TextWriter _log;
        public MySyncContext(TextWriter log)
        {
            _previousContext = Current;
            _log = log;
            SetSynchronizationContext(this);
        }
        public int OpCount => Volatile.Read(ref _opCount);
        private int _opCount;
        private void Incr() => Interlocked.Increment(ref _opCount);

        public void Reset() => Volatile.Write(ref _opCount, 0);

        public override string ToString() => $"Sync context ({(IsCurrent ? "active" : "inactive")}): {OpCount}";

        void IDisposable.Dispose() => SetSynchronizationContext(_previousContext);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Log(_log, $"sync-ctx: Post {Format(d, state)}");
            Incr();
            ThreadPool.QueueUserWorkItem(
                static state =>
                {
                    var tuple = (Tuple<MySyncContext, SendOrPostCallback, object?>)state!;
                    tuple.Item1.Invoke(tuple.Item2, tuple.Item3);
                },
                Tuple.Create<MySyncContext, SendOrPostCallback, object?>(this, d, state));
        }

        private void Invoke(SendOrPostCallback d, object? state)
        {
            Log(_log, $"sync-ctx: Invoke {Format(d, state)}");
            if (!IsCurrent) SetSynchronizationContext(this);
            d(state);
        }

        private static string Format(SendOrPostCallback? d, object? state)
        {
            if (d is null) return "";
            string name = d.IsSingle() ? d.Method.Name : GetNames(d);
            return state is null ? name : $"{name}:{state}";

            static string GetNames(SendOrPostCallback d)
            {
                var sb = new StringBuilder();
                foreach (var x in d.AsEnumerable())
                {
                    if (sb.Length != 0) sb.Append(",");
                    sb.Append(x.Method.Name);
                }
                return sb.ToString();
            }
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            Log(_log, $"sync-ctx: Send {Format(d, state)}");
            Incr();
            Invoke(d, state);
        }

        public bool IsCurrent => ReferenceEquals(this, Current);

        public override int Wait(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout)
        {
            Incr();
            return base.Wait(waitHandles, waitAll, millisecondsTimeout);
        }
        public override void OperationStarted()
        {
            Incr();
            base.OperationStarted();
        }
        public override void OperationCompleted()
        {
            Incr();
            base.OperationCompleted();
        }
    }
}

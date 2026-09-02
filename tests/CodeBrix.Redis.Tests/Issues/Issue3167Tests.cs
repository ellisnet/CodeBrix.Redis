using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests.Issues; //was previously: StackExchange.Redis.Tests.Issues;

/// <summary>
/// A write can lose its connection at any point: <c>Shutdown</c> discards the output writer, and it cannot wait
/// for the write lock, because teardown is usually spotted on the read loop - which must not block. That is an
/// ordinary closure, so it should be reported as a connection failure, not as <c>InternalFailure</c> carrying
/// <c>InvalidOperationException("Output pipe not initialized")</c>, which reads as a client bug and is also
/// announced through <see cref="IConnectionMultiplexer.InternalError"/>. See #3167.
/// </summary>
[Collection(NonParallelCollection.Name)] // these kill the connection out from under a shared server
public class Issue3167Tests(ITestOutputHelper output) : TestBase(output)
{
    protected override string GetConfiguration() => TestConfig.Current.PrimaryServerAndPort;

    /// <summary>
    /// The deterministic half: teardown has already discarded the output writer by the time the write starts,
    /// which is exactly what the race produces, minus the racing.
    /// </summary>
    [Fact]
    public async Task write_losing_its_connection_is_reported_as_a_closure()
    {
        try
        {
            await using var conn = Create(shared: false);
            await conn.GetDatabase().PingAsync();

            var bridge = conn.GetServerSnapshot()[0].GetBridge(ConnectionType.Interactive);
            Assert.NotNull(bridge); //kept as the xUnit form: it carries [NotNull], so the compiler's null-state flows to the dereference below
            var physical = bridge.TryConnect(null);
            Assert.NotNull(physical);
            var internalErrors = new List<Exception>();
            conn.UnderlyingMultiplexer.InternalError += (_, args) => internalErrors.Add(args.Exception);

            // teardown wins the race
            physical.Shutdown();

            var message = Message.Create(0, CommandFlags.None, RedisCommand.GET, (RedisKey)Me());
            var resultBox = SimpleResultBox<string>.Create();
            message.SetSource(ResultProcessor.String, resultBox);

            var result = await bridge.WriteMessageTakingWriteLockAsync(physical, message, bypassBacklog: true);
            result.Should().Be(WriteResult.WriteFailure);

            resultBox.GetResult(out var ex);
            Assert.NotNull(ex); //kept as the xUnit form: it carries [NotNull], so the compiler's null-state flows to the dereference below
            Log(ex.ToString());

            // Note the message can also be completed by the teardown that our own Shutdown kicked off, carrying
            // the underlying socket error - that is fine, and is why this doesn't demand one exact exception. What
            // must not happen is the write path reporting a closure as an internal fault: before #3167 this was
            // "InternalFailure on [0]:GET ...", wrapping InvalidOperationException("Output pipe not initialized").
            Assert.DoesNotContain("Output pipe not initialized", ex.ToString(), StringComparison.Ordinal);
            for (Exception? walk = ex; walk is not null; walk = walk.InnerException)
            {
                if (walk is RedisConnectionException rce)
                {
                    rce.FailureType.Should().NotBe(ConnectionFailureType.InternalFailure);
                }
            }

            // ...and a routine disconnect should not be announced as an internal library fault
            internalErrors.Should().BeEmpty();
        }
        finally
        {
            ClearAmbientFailures();
        }
    }

    /// <summary>
    /// The racing half: writes in flight while the connection is repeatedly torn down underneath them. This is what
    /// found the issue, and it covers the whole write path rather than one throw site - including the backlog drain,
    /// which the deterministic test above does not reach.
    /// </summary>
    /// <remarks>
    /// Explicit: it needs to saturate the box to hit the window reliably, which starves anything running alongside
    /// it. Run it directly when touching the write path, via
    /// <c>dotnet run -c Release -f net10.0 -- -explicit only -method "*WritesRacingTeardown*"</c>. Note this may
    /// show as Inconclusive, depending on the runner.
    /// </remarks>
    [Fact(Explicit = true)]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task writes_racing_teardown_are_never_internal_failures()
    {
        var options = new ConfigurationOptions
        {
            BacklogPolicy = BacklogPolicy.Default,
            AbortOnConnectFail = false,
            ConnectTimeout = 1000,
            ConnectRetry = 2,
            SyncTimeout = 5000,
            AsyncTimeout = 5000,
            KeepAlive = 10000,
            AllowAdmin = true,
            AllowSimulateConnectionFailure = true,
        };
        options.EndPoints.Add(TestConfig.Current.PrimaryServerAndPort);

        try
        {
            await using var conn = await ConnectionMultiplexer.ConnectAsync(options, Writer);
            var db = conn.GetDatabase();
            await db.PingAsync();

            var server = conn.GetServerSnapshot()[0];
            Assert.SkipUnless(server.CanSimulateConnectionFailure, "Skipping because server cannot simulate connection failure");

            var outputPipeFaults = new ConcurrentQueue<Exception>();
            var internalFailures = new ConcurrentQueue<Exception>();
            var internalErrors = new ConcurrentQueue<Exception>();
            long totalOps = 0, totalFaults = 0;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            void Record(Exception ex)
            {
                Interlocked.Increment(ref totalFaults);

                // the bug's fingerprint: the write path complaining as if we had never connected
                if (ex.ToString().Contains("Output pipe not initialized", StringComparison.Ordinal))
                {
                    outputPipeFaults.Enqueue(ex);
                }

                for (Exception? walk = ex; walk is not null; walk = walk.InnerException)
                {
                    if (walk is RedisConnectionException { FailureType: ConnectionFailureType.InternalFailure })
                    {
                        internalFailures.Enqueue(ex);
                    }
                }
            }

            conn.InternalError += (_, args) =>
            {
                internalErrors.Enqueue(args.Exception);
                Record(args.Exception);
            };

            var key = Me();
            var token = cts.Token;

            // lots of concurrent writers, so the write lock is contended and a backlog forms
            var writers = Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await db.StringGetAsync(key).ForAwait();
                        Interlocked.Increment(ref totalOps);
                    }
                    catch (Exception ex)
                    {
                        Record(ex);
                    }
                }
            })).ToArray();

            // ...while the connection is repeatedly torn down under them. SimulateConnectionFailure runs
            // RecordConnectionFailed -> Shutdown() synchronously on *this* thread, which is what discards the
            // output writer, so it lands while the writers are inside the write lock.
            int kills = 0;
            while (!token.IsCancellationRequested)
            {
                server.SimulateConnectionFailure(SimulatedFailureType.AllInteractive);
                kills++;
                try
                {
                    await Task.Delay(20, token).ForAwait();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            await Task.WhenAll(writers).ForAwait();

            var ops = Volatile.Read(ref totalOps);
            Log($"ops: {ops}, faults: {Volatile.Read(ref totalFaults)}, kills: {kills}");
            Log($"internal failures: {internalFailures.Count}, output-pipe faults: {outputPipeFaults.Count}, internal errors: {internalErrors.Count}");

            void Dump(string label, ConcurrentQueue<Exception> queue)
            {
                foreach (var ex in queue.Take(3))
                {
                    Log($"---- {label} ----");
                    Log(ex.ToString());
                }
            }

            Dump("output-pipe fault", outputPipeFaults);
            Dump("internal failure", internalFailures);

            // this only means anything if we actually raced teardown; if these trip, the writers or the kill
            // loop stopped doing their job, rather than the bug being fixed
            (kills > 10).Should().BeTrue($"expected the kill loop to run, got {kills}");
            (ops > 1000).Should().BeTrue($"expected the writers to run, got {ops} ops");
            (Volatile.Read(ref totalFaults) > 0).Should().BeTrue("expected the teardowns to have visible fallout");

            outputPipeFaults.Should().BeEmpty();
            internalFailures.Should().BeEmpty();
        }
        finally
        {
            ClearAmbientFailures();
        }
    }
}

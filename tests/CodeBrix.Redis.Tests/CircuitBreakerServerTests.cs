using System;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Redis.Availability;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class CircuitBreakerServerTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task circuit_breaker_observes_message_results()
    {
        using var server = new InProcessTestServer();

        // take the template options from the server, and slot in our test breaker *before* connecting,
        // so every physical connection yanks an accumulator from it during init
        var config = server.GetClientConfig();
        var breaker = new CountingCircuitBreaker();
        config.CircuitBreaker = breaker;

        using var client = await ConnectionMultiplexer.ConnectAsync(config);
        var db = client.GetDatabase();

        // some successful operations (these, plus handshake traffic, count as non-fault observations)
        RedisKey key = Me();
        await db.StringSetAsync(key, "abc");
        (await db.StringGetAsync(key)).Should().Be("abc");
        await db.StringGetAsync(key);

        var successesAfterGetSets = breaker.Successes;

        // knock the server offline: it now replies LOADING to every command, which (unlike an
        // application-level "unknown command") is a genuine availability fault the breaker observes.
        // flip it straight back off so no background heartbeat can observe a second LOADING reply -
        // the fault for our command is recorded synchronously as it completes, before the await returns.
        server.IsLoading = true;
        var fault = await Assert.ThrowsAsync<RedisServerException>(() => db.StringGetAsync(key));
        server.IsLoading = false;
        fault.Kind.Should().Be(RedisErrorKind.Loading);
        Output.WriteLine($"loading fault: {fault.GetType().Name}: {fault.Message}");

        Output.WriteLine($"observed successes={breaker.Successes}, failures={breaker.Failures}, lastFault={breaker.LastFault?.GetType().Name}");

        // the get/sets were observed as successes
        (successesAfterGetSets > 0).Should().BeTrue("expected the successful operations to be observed");

        // exactly one fault (the LOADING reply), captured as the clean server error. A healthy
        // breaker must NOT tear the connection down: a regression there shows up here as a
        // RedisConnectionException (and extra faults) rather than this RedisServerException.
        breaker.Failures.Should().Be(1);
        var serverFault = Assert.IsType<RedisServerException>(breaker.LastFault);
        serverFault.Kind.Should().Be(RedisErrorKind.Loading);
    }

    // a minimal breaker for tests: shares counters across all accumulators it creates, so we can
    // observe traffic across every physical connection; it never trips (always reports healthy)
    private sealed class CountingCircuitBreaker : CircuitBreaker
    {
        private int _successes, _failures;

        public int Successes => Volatile.Read(ref _successes);
        public int Failures => Volatile.Read(ref _failures);
        public Exception? LastFault { get; private set; }

        public override Accumulator CreateAccumulator() => new CountingAccumulator(this);

        private sealed class CountingAccumulator(CountingCircuitBreaker owner) : Accumulator
        {
            public override void ObserveResult(in FaultContext context)
            {
                if (context.IsFault)
                {
                    Interlocked.Increment(ref owner._failures);
                    owner.LastFault = context.Fault;
                }
                else
                {
                    Interlocked.Increment(ref owner._successes);
                }
            }

            public override bool IsHealthy() => true;

            public override void Reset() { }
        }
    }
}

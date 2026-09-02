using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Redis.RedLock;
using CodeBrix.Redis.RedLock.Configuration;
using CodeBrix.Redis.RedLock.Util;
using CodeBrix.Redis.Testing.Topologies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: RedLockNet.Tests;
//RedLock/ is a FOLDER ONLY, not a sub-namespace - the same pattern the client core uses for
//APITypes/ and Enums/. A namespace segment literally named RedLock would make the simple name
//`RedLock` resolve to the namespace CodeBrix.Redis.RedLock instead of the type of that name
//(CS0118), which is why the cast below is fully qualified.

/// <summary>
/// The Redlock tests, converted from upstream's NUnit fixture.
/// </summary>
/// <remarks>
/// <para>
/// Two things changed relative to upstream, both forced by where the servers now come from.
/// </para>
/// <para>
/// FIRST, THE ENDPOINTS. Upstream hard-coded <c>localhost:6379/6380/6381</c> as three "active"
/// servers a developer was expected to start by hand. In this repository the servers come from
/// <c>CodeBrix.Redis.TestHarness</c>, where 6380 is the basic server's READ-ONLY REPLICA and 6381
/// is the PASSWORD-PROTECTED server - so upstream's three endpoints cannot form a Redlock quorum
/// here: a replica refuses the <c>SET .. NX</c> and an unauthenticated connection to 6381 is
/// rejected. The three active endpoints are therefore the harness's three independent, writable
/// primaries: the basic primary (6379), the failover primary (6382) and the sentinel primary
/// (7010). The inactive endpoints keep upstream's deliberately dead ports.
/// </para>
/// <para>
/// SECOND, THE ISOLATION. Upstream ran this fixture as its own project against its own servers.
/// Here it shares the harness with tests that deliberately demote 6382 and 7010
/// (<c>FailoverTests</c>, <c>SentinelFailoverTests</c>), and those already sit in
/// <see cref="NonParallelCollection"/>; joining that collection is what keeps a Redlock quorum
/// from evaporating mid-test because another collection is exercising a failover.
/// </para>
/// <para>
/// Everything each test asserts is upstream's, unchanged.
/// </para>
/// </remarks>
[Collection(NonParallelCollection.Name)]
public class RedLockTests : IDisposable
{
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;

    //was previously: an NUnit [OneTimeSetUp] method. xUnit builds one instance per test, so the
    //setup is the constructor. The logger factory is the no-op one: upstream built a console
    //logger with Microsoft.Extensions.Logging.Console, a package this repository does not take.
    public RedLockTests()
    {
        ThreadPool.SetMinThreads(100, 100);

        loggerFactory = NullLoggerFactory.Instance;
        logger = loggerFactory.CreateLogger<RedLockTests>();
    }

    /// <inheritdoc />
    //was previously: an NUnit [OneTimeTearDown]-shaped disposal; there is nothing to release now
    //that the logger factory is the shared no-op instance, but the shape is kept so a future
    //factory with real state has somewhere to go.
    public void Dispose() => GC.SuppressFinalize(this);

    // the harness's three independent writable primaries; see the class remarks
    private static readonly EndPoint ActiveServer1 =
        new DnsEndPoint(TestConfig.Current.PrimaryServer, TestConfig.Current.PrimaryPort);
    private static readonly EndPoint ActiveServer2 =
        new DnsEndPoint(TestConfig.Current.FailoverPrimaryServer, TestConfig.Current.FailoverPrimaryPort);
    private static readonly EndPoint ActiveServer3 =
        new DnsEndPoint(TestConfig.Current.SentinelServer, SentinelTopology.PrimaryPort);

    // make sure redis isn't running on these
    private static readonly EndPoint InactiveServer1 = new DnsEndPoint("localhost", 63790);
    private static readonly EndPoint InactiveServer2 = new DnsEndPoint("localhost", 63791);
    private static readonly EndPoint InactiveServer3 = new DnsEndPoint("localhost", 63791);

    // make sure redis is running here with the specified password
    //was previously: localhost:6382 with password "password". 6382 is the harness's failover
    //primary; its password-protected server is 6381.
    private static readonly RedLockEndPoint PasswordedServer = new RedLockEndPoint
    {
        EndPoint = new DnsEndPoint(TestConfig.Current.SecureServer, TestConfig.Current.SecurePort),
        Password = TestConfig.Current.SecurePassword,
    };

    private static readonly RedLockEndPoint NonDefaultDatabaseServer = new RedLockEndPoint
    {
        EndPoint = ActiveServer1,
        RedisDatabase = 1,
    };

    private static readonly RedLockEndPoint NonDefaultRedisKeyFormatServer = new RedLockEndPoint
    {
        EndPoint = ActiveServer1,
        RedisKeyFormat = "{0}-redislock",
    };

    private static readonly IList<RedLockEndPoint> AllActiveEndPoints = new List<RedLockEndPoint>
    {
        ActiveServer1,
        ActiveServer2,
        ActiveServer3,
    };

    private static readonly IList<RedLockEndPoint> AllInactiveEndPoints = new List<RedLockEndPoint>
    {
        InactiveServer1,
        InactiveServer2,
        InactiveServer3,
    };

    private static readonly IList<RedLockEndPoint> SomeActiveEndPointsWithQuorum = new List<RedLockEndPoint>
    {
        ActiveServer1,
        ActiveServer2,
        ActiveServer3,
        InactiveServer1,
        InactiveServer2,
    };

    private static readonly IList<RedLockEndPoint> SomeActiveEndPointsWithNoQuorum = new List<RedLockEndPoint>
    {
        ActiveServer1,
        ActiveServer2,
        ActiveServer3,
        InactiveServer1,
        InactiveServer2,
        InactiveServer3,
    };

    [Fact]
    public void single_lock_is_acquired()
    {
        //Arrange
        Skip.IfNoContainers();

        //Act & Assert
        CheckSingleRedisLock(
            () => RedLockFactory.Create(SomeActiveEndPointsWithQuorum, loggerFactory),
            RedLockStatus.Acquired);
    }

    [Fact]
    public async Task single_lock_is_acquired_async()
    {
        //Arrange
        Skip.IfNoContainers();

        //Act & Assert
        await CheckSingleRedisLockAsync(
            () => RedLockFactory.Create(SomeActiveEndPointsWithQuorum, loggerFactory),
            RedLockStatus.Acquired);
    }

    [Fact]
    public void overlapping_locks_conflict()
    {
        //Arrange
        Skip.IfNoContainers();
        using var redisLockFactory = RedLockFactory.Create(AllActiveEndPoints, loggerFactory);
        var resource = $"testredislock:{Guid.NewGuid()}";

        //Act & Assert
        using (var firstLock = redisLockFactory.CreateLock(resource, TimeSpan.FromSeconds(30)))
        {
            firstLock.IsAcquired.Should().BeTrue();

            using (var secondLock = redisLockFactory.CreateLock(resource, TimeSpan.FromSeconds(30)))
            {
                secondLock.IsAcquired.Should().BeFalse();
                secondLock.Status.Should().Be(RedLockStatus.Conflicted);
            }
        }
    }

    [Fact]
    public async Task overlapping_locks_conflict_async()
    {
        //Arrange
        Skip.IfNoContainers();
        var task = DoOverlappingLocksAsync();

        //Act
        logger.LogInformation("======================================================");

        //Assert
        await task;
    }

    private async Task DoOverlappingLocksAsync()
    {
        using var redisLockFactory = RedLockFactory.Create(AllActiveEndPoints, loggerFactory);
        var resource = $"testredislock:{Guid.NewGuid()}";

        await using (var firstLock = await redisLockFactory.CreateLockAsync(resource, TimeSpan.FromSeconds(30)))
        {
            firstLock.IsAcquired.Should().BeTrue();

            await using (var secondLock = await redisLockFactory.CreateLockAsync(resource, TimeSpan.FromSeconds(30)))
            {
                secondLock.IsAcquired.Should().BeFalse();
                secondLock.Status.Should().Be(RedLockStatus.Conflicted);
            }
        }
    }

    [Fact]
    public void blocking_concurrent_locks_are_both_acquired_in_turn()
    {
        //Arrange
        Skip.IfNoContainers();
        var locksAcquired = 0;

        //Act
        using (var redisLockFactory = RedLockFactory.Create(AllActiveEndPoints, loggerFactory))
        {
            var resource = $"testblockingconcurrentlocks:{Guid.NewGuid()}";

            var threads = new List<Thread>();

            for (var i = 0; i < 2; i++)
            {
                var thread = new Thread(() =>
                {
                    // ReSharper disable once AccessToDisposedClosure (we join on threads before disposing)
                    using (var redisLock = redisLockFactory.CreateLock(
                        resource,
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromSeconds(0.5)))
                    {
                        logger.LogInformation("Entering lock");
                        if (redisLock.IsAcquired)
                        {
                            Interlocked.Increment(ref locksAcquired);
                        }
                        Thread.Sleep(4000);
                        logger.LogInformation("Leaving lock");
                    }
                });

                thread.Start();

                threads.Add(thread);
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }
        }

        //Assert
        locksAcquired.Should().Be(2);
    }

    [Fact]
    public void sequential_locks_are_both_acquired()
    {
        //Arrange
        Skip.IfNoContainers();
        using var redisLockFactory = RedLockFactory.Create(AllActiveEndPoints, loggerFactory);
        var resource = $"testredislock:{Guid.NewGuid()}";

        //Act & Assert
        using (var firstLock = redisLockFactory.CreateLock(resource, TimeSpan.FromSeconds(30)))
        {
            firstLock.IsAcquired.Should().BeTrue();
        }

        using (var secondLock = redisLockFactory.CreateLock(resource, TimeSpan.FromSeconds(30)))
        {
            secondLock.IsAcquired.Should().BeTrue();
        }
    }

    [Fact]
    public void held_lock_is_renewed_while_it_is_held()
    {
        //Arrange
        Skip.IfNoContainers();
        using var redisLockFactory = RedLockFactory.Create(AllActiveEndPoints, loggerFactory);
        var resource = $"testrenewinglock:{Guid.NewGuid()}";
        int extendCount;

        //Act
        using (var redisLock = redisLockFactory.CreateLock(resource, TimeSpan.FromSeconds(2)))
        {
            redisLock.IsAcquired.Should().BeTrue();

            Thread.Sleep(4000);

            extendCount = redisLock.ExtendCount;
        }

        //Assert
        extendCount.Should().BeGreaterThan(2);
    }

    [Fact]
    public async Task contended_extend_is_cancelled_when_the_lock_is_released()
    {
        //Arrange
        Skip.IfNoContainers();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var redisLockFactory = RedLockFactory.Create(
            new List<RedLockEndPoint> { ActiveServer1 },
            loggerFactory);
        var resource = $"testcontendedlock:{Guid.NewGuid()}";

        //Act
        var tasks = new List<Task>
        {
            Task.Run(
                () => ContendedSleep(redisLockFactory, resource, 1, TimeSpan.FromSeconds(2)),
                cancellationToken),
        };

        // sleep for just shorter than the duration of the previous lock, so that the second lock should fail to be acquired on the first attempt but successfully acquired on a retry
        await Task.Delay(TimeSpan.FromSeconds(1.99), cancellationToken);

        tasks.Add(Task.Run(
            () => ContendedSleep(redisLockFactory, resource, 2, TimeSpan.FromSeconds(2)),
            cancellationToken));

        //Assert
        await Task.WhenAll(tasks);
    }

    private async Task ContendedSleep(RedLockFactory redisLockFactory, string resource, int i, TimeSpan duration)
    {
        logger.LogInformation("Starting task {i}", i);

        IRedLock redlock;
        var acquired = false;
        await using (redlock = await redisLockFactory.CreateLockAsync(resource, duration))
        {
            if (redlock.IsAcquired)
            {
                acquired = true;
                await Task.Delay(duration, TestContext.Current.CancellationToken);
            }
        }

        logger.LogInformation("Ending task {i}, acquired: {acquired}, extendCount: {extendCount}", i, acquired, redlock.ExtendCount);

        acquired.Should().BeTrue();
        redlock.ExtendCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void lock_is_released_after_its_timeout()
    {
        //Arrange
        Skip.IfNoContainers();
        using var lockFactory = RedLockFactory.Create(AllActiveEndPoints, loggerFactory);
        var resource = $"testrenewinglock:{Guid.NewGuid()}";

        //Act & Assert
        using (var firstLock = lockFactory.CreateLock(resource, TimeSpan.FromSeconds(1)))
        {
            firstLock.IsAcquired.Should().BeTrue();

            Thread.Sleep(550); // should cause keep alive timer to fire once
            ((global::CodeBrix.Redis.RedLock.RedLock)firstLock).StopKeepAliveTimer(); // stop the keep alive timer to simulate process crash
            Thread.Sleep(1200); // wait until the key expires from redis

            using (var secondLock = lockFactory.CreateLock(resource, TimeSpan.FromSeconds(1)))
            {
                secondLock.IsAcquired.Should().BeTrue(); // Eventually the outer lock should timeout
            }
        }
    }

    [Fact]
    public void quorum_status_follows_the_number_of_active_endpoints()
    {
        //Arrange
        Skip.IfNoContainers();

        //Act & Assert
        logger.LogInformation("======== Testing quorum with all active endpoints ========");
        CheckSingleRedisLock(
            () => RedLockFactory.Create(AllActiveEndPoints, loggerFactory),
            RedLockStatus.Acquired);
        logger.LogInformation("======== Testing quorum with no active endpoints ========");
        CheckSingleRedisLock(
            () => RedLockFactory.Create(AllInactiveEndPoints, loggerFactory),
            RedLockStatus.NoQuorum);
        logger.LogInformation("======== Testing quorum with enough active endpoints ========");
        CheckSingleRedisLock(
            () => RedLockFactory.Create(SomeActiveEndPointsWithQuorum, loggerFactory),
            RedLockStatus.Acquired);
        logger.LogInformation("======== Testing quorum with not enough active endpoints ========");
        CheckSingleRedisLock(
            () => RedLockFactory.Create(SomeActiveEndPointsWithNoQuorum, loggerFactory),
            RedLockStatus.NoQuorum);
    }

    [Fact]
    public void race_for_quorum_repeated()
    {
        //Arrange
        Skip.IfNoContainers();

        //Act & Assert
        for (var i = 0; i < 2; i++)
        {
            logger.LogInformation($"======== Start test {i} ========");

            race_for_quorum_gives_the_lock_to_exactly_one();
        }
    }

    [Fact]
    public void race_for_quorum_gives_the_lock_to_exactly_one()
    {
        //Arrange
        Skip.IfNoContainers();
        var cancellationToken = TestContext.Current.CancellationToken;
        var locksAcquired = 0;

        var lockKey = $"testredislock:{ThreadSafeRandom.Next(10000)}";

        var tasks = new List<Task>();

        //Act
        for (var i = 0; i < 3; i++)
        {
            var task = new Task(() =>
            {
                logger.LogDebug("Starting task");

                using (var redisLockFactory = RedLockFactory.Create(AllActiveEndPoints, loggerFactory))
                {
                    var sw = Stopwatch.StartNew();

                    using (var redisLock = redisLockFactory.CreateLock(lockKey, TimeSpan.FromSeconds(30)))
                    {
                        sw.Stop();

                        logger.LogDebug($"Lock method took {sw.ElapsedMilliseconds}ms to return, IsAcquired = {redisLock.IsAcquired}");

                        if (redisLock.IsAcquired)
                        {
                            logger.LogDebug($"Got lock with id {redisLock.LockId}, sleeping for a bit");

                            Interlocked.Increment(ref locksAcquired);

                            // Sleep for long enough for the other threads to give up
                            //Thread.Sleep(TimeSpan.FromSeconds(2));
                            Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).Wait(cancellationToken);

                            logger.LogDebug($"Lock with id {redisLock.LockId} done sleeping");
                        }
                        else
                        {
                            logger.LogDebug("Couldn't get lock, giving up");
                        }
                    }
                }
            }, TaskCreationOptions.LongRunning);

            tasks.Add(task);
        }

        foreach (var task in tasks)
        {
            task.Start();
        }

        Task.WaitAll([.. tasks], cancellationToken);

        //Assert
        locksAcquired.Should().Be(1);
    }

    [Fact]
    public void password_protected_connection_acquires()
    {
        //Arrange
        Skip.IfNoContainers();

        //Act & Assert
        CheckSingleRedisLock(
            () => RedLockFactory.Create(new List<RedLockEndPoint> { PasswordedServer }, loggerFactory),
            RedLockStatus.Acquired);
    }

    //was previously: [Ignore("Requires a redis server that supports SSL")]. The harness DOES publish
    //a TLS endpoint, but its authority is generated per run and is not in any trust store, so a
    //RedLockEndPoint with Ssl=true still cannot validate it - the test stays skipped, for upstream's
    //reason, and the port is the harness's so it is not pointing at a stale one.
    [Fact]
    public void ssl_connection_acquires()
    {
        Assert.Skip("Requires a redis server that supports SSL");

        //Arrange
        var endPoint = new RedLockEndPoint
        {
            EndPoint = new DnsEndPoint(TestConfig.Current.SslServer!, TestConfig.Current.SslPort),
            Ssl = true,
        };

        //Act & Assert
        CheckSingleRedisLock(
            () => RedLockFactory.Create(new List<RedLockEndPoint> { endPoint }, loggerFactory),
            RedLockStatus.Acquired);
    }

    [Fact]
    public void ssl_connection_with_an_explicit_protocol_acquires()
    {
        Assert.Skip("Requires a redis server that supports SSL and TLS 1.2");

        //Arrange
        var endPoint = new RedLockEndPoint
        {
            EndPoint = new DnsEndPoint(TestConfig.Current.SslServer!, TestConfig.Current.SslPort),
            Ssl = true,
            SslProtocols = SslProtocols.Tls12,
        };

        //Act & Assert
        CheckSingleRedisLock(
            () => RedLockFactory.Create(new List<RedLockEndPoint> { endPoint }, loggerFactory),
            RedLockStatus.Acquired);
    }

    [Fact]
    public void non_default_redis_database_acquires()
    {
        //Arrange
        Skip.IfNoContainers();

        //Act & Assert
        CheckSingleRedisLock(
            () => RedLockFactory.Create(new List<RedLockEndPoint> { NonDefaultDatabaseServer }, loggerFactory),
            RedLockStatus.Acquired);
    }

    [Fact]
    public void non_default_redis_key_format_acquires()
    {
        //Arrange
        Skip.IfNoContainers();

        //Act & Assert
        CheckSingleRedisLock(
            () => RedLockFactory.Create(new List<RedLockEndPoint> { NonDefaultRedisKeyFormatServer }, loggerFactory),
            RedLockStatus.Acquired);
    }

    //was previously: the [InstantHandle] parameter attribute, from JetBrains.Annotations - a
    //third-party package this repository does not take. It was a ReSharper hint only.
    private static void CheckSingleRedisLock(Func<RedLockFactory> factoryBuilder, RedLockStatus expectedStatus)
    {
        using (var redisLockFactory = factoryBuilder())
        {
            var resource = $"testredislock:{Guid.NewGuid()}";

            using (var redisLock = redisLockFactory.CreateLock(resource, TimeSpan.FromSeconds(30)))
            {
                redisLock.IsAcquired.Should().Be(expectedStatus == RedLockStatus.Acquired);
                redisLock.Status.Should().Be(expectedStatus);
            }
        }
    }

    private static async Task CheckSingleRedisLockAsync(Func<RedLockFactory> factoryBuilder, RedLockStatus expectedStatus)
    {
        using (var redisLockFactory = factoryBuilder())
        {
            var resource = $"testredislock:{Guid.NewGuid()}";

            await using (var redisLock = await redisLockFactory.CreateLockAsync(resource, TimeSpan.FromSeconds(30)))
            {
                redisLock.IsAcquired.Should().Be(expectedStatus == RedLockStatus.Acquired);
                redisLock.Status.Should().Be(expectedStatus);
            }
        }
    }

    [Fact]
    public void blocking_lock_wait_is_cancelled()
    {
        //Arrange
        Skip.IfNoContainers();
        using var cts = new CancellationTokenSource();
        var resource = $"testredislock:{Guid.NewGuid()}";
        using var redisLockFactory = RedLockFactory.Create(AllActiveEndPoints, loggerFactory);

        //Act & Assert
        using (var firstLock = redisLockFactory.CreateLock(
            resource,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(1)))
        {
            firstLock.IsAcquired.Should().BeTrue();

            cts.CancelAfter(TimeSpan.FromSeconds(2));

            Assert.Throws<OperationCanceledException>(() =>
            {
                using (var secondLock = redisLockFactory.CreateLock(
                    resource,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(1),
                    cts.Token))
                {
                    // should never get here
                    Assert.Fail("The blocking wait was cancelled, so the second lock must never be created.");
                }
            });
        }
    }

    [Fact]
    public void factory_requires_at_least_one_endpoint()
    {
        //Act & Assert
        Assert.Throws<ArgumentException>(() =>
        {
            using (var redisLockFactory = RedLockFactory.Create(new List<RedLockEndPoint>(), loggerFactory))
            {
            }
        });

        Assert.Throws<ArgumentException>(() =>
        {
            using (var redisLockFactory = RedLockFactory.Create((IList<RedLockEndPoint>)null!, loggerFactory))
            {
            }
        });
    }

    [Fact]
    public void existing_multiplexer_acquires()
    {
        //Arrange
        Skip.IfNoContainers();
        using var connectionMultiplexer = ConnectionMultiplexer.Connect(new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            EndPoints = { ActiveServer1 },
        });

        //Act & Assert
        CheckSingleRedisLock(
            () => RedLockFactory.Create(new List<RedLockMultiplexer> { connectionMultiplexer }, loggerFactory),
            RedLockStatus.Acquired);
    }

    [Fact]
    public async Task time_lock()
    {
        Assert.Skip("Timing test");

        //Arrange
        using var redisLockFactory = RedLockFactory.Create(AllActiveEndPoints, loggerFactory);
        var resource = $"testredislock:{Guid.NewGuid()}";

        // warmup
        for (var i = 0; i < 10; i++)
        {
            await using (await redisLockFactory.CreateLockAsync(resource, TimeSpan.FromSeconds(30)))
            {
            }
        }

        var sw = new Stopwatch();
        var totalAcquire = new TimeSpan();
        var totalRelease = new TimeSpan();
        var iterations = 10000;

        //Act & Assert
        for (var i = 0; i < iterations; i++)
        {
            sw.Restart();

            await using (var redisLock = await redisLockFactory.CreateLockAsync(resource, TimeSpan.FromSeconds(30)))
            {
                sw.Stop();

                redisLock.IsAcquired.Should().BeTrue();

                logger.LogInformation($"Acquire {i} took {sw.ElapsedTicks} ticks, status: {redisLock.Status}");
                totalAcquire += sw.Elapsed;

                sw.Restart();
            }

            sw.Stop();

            logger.LogInformation($"Release {i} took {sw.ElapsedTicks} ticks, success");
            totalRelease += sw.Elapsed;
        }

        logger.LogWarning($"{iterations} iterations, total acquire time: {totalAcquire}, total release time {totalRelease}");
    }
}

using System;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using CodeBrix.Redis.Configuration;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class ConnectionFailureErrorsTests(ITestOutputHelper output) : TestBase(output)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ssl_certificate_validation_error(bool isCertValidationSucceeded)
    {
        Skip.IfNoConfig(nameof(TestConfig.Config.AzureCacheServer), TestConfig.Current.AzureCacheServer);
        Skip.IfNoConfig(nameof(TestConfig.Config.AzureCachePassword), TestConfig.Current.AzureCachePassword);

        var options = new ConfigurationOptions();
        options.EndPoints.Add(TestConfig.Current.AzureCacheServer);
        options.Ssl = true;
        options.Password = TestConfig.Current.AzureCachePassword;
        options.CertificateValidation += (sender, cert, chain, errors) => isCertValidationSucceeded;
        options.AbortOnConnectFail = false;

        await using var conn = await ConnectionMultiplexer.ConnectAsync(options);

        await RunBlockingSynchronousWithExtraThreadAsync(InnerScenario).ForAwait();

        void InnerScenario()
        {
            conn.ConnectionFailed += (sender, e) =>
                e.FailureType.Should().Be(ConnectionFailureType.AuthenticationFailure);
            if (!isCertValidationSucceeded)
            {
                // Validate that in this case it throws an certificatevalidation exception
                var outer = Assert.Throws<RedisConnectionException>(() => conn.GetDatabase().Ping());
                outer.FailureType.Should().Be(ConnectionFailureType.UnableToResolvePhysicalConnection);

                Assert.NotNull(outer.InnerException);
                var inner = Assert.IsType<RedisConnectionException>(outer.InnerException);
                inner.FailureType.Should().Be(ConnectionFailureType.AuthenticationFailure);

                Assert.NotNull(inner.InnerException);
                var innerMost = Assert.IsType<AuthenticationException>(inner.InnerException);
                innerMost.Message.Should().Be("The remote certificate is invalid according to the validation procedure.");
            }
            else
            {
                conn.GetDatabase().Ping();
            }
        }

        // wait for a second for connectionfailed event to fire
        await Task.Delay(1000, TestContext.Current.CancellationToken).ForAwait();
    }

    [Fact]
    public async Task authentication_failure_error()
    {
        Skip.IfNoConfig(nameof(TestConfig.Config.AzureCacheServer), TestConfig.Current.AzureCacheServer);

        var options = new ConfigurationOptions();
        options.EndPoints.Add(TestConfig.Current.AzureCacheServer);
        options.Ssl = true;
        options.Password = "";
        options.AbortOnConnectFail = false;
        options.CertificateValidation += SSLTests.ShowCertFailures(Writer);

        await using var conn = await ConnectionMultiplexer.ConnectAsync(options);

        await RunBlockingSynchronousWithExtraThreadAsync(InnerScenario).ForAwait();
        void InnerScenario()
        {
            conn.ConnectionFailed += (sender, e) =>
            {
                if (e.FailureType == ConnectionFailureType.SocketFailure) Assert.Skip("socket fail"); // this is OK too
                e.FailureType.Should().Be(ConnectionFailureType.AuthenticationFailure);
            };
            var ex = Assert.Throws<RedisConnectionException>(() => conn.GetDatabase().Ping());

            Assert.NotNull(ex.InnerException);
            var rde = Assert.IsType<RedisConnectionException>(ex.InnerException);
            ex.CommandStatus.Should().Be(CommandStatus.WaitingToBeSent);
            rde.FailureType.Should().Be(ConnectionFailureType.AuthenticationFailure);
            Assert.NotNull(rde.InnerException);
            rde.InnerException.Message.Should().Be("Error: NOAUTH Authentication required. Verify if the Redis password provided is correct.");
        }

        // Wait for a second  for connectionfailed event to fire
        await Task.Delay(1000, TestContext.Current.CancellationToken).ForAwait();
    }

    [Fact]
    public async Task socket_failure_error()
    {
        await RunBlockingSynchronousWithExtraThreadAsync(InnerScenario).ForAwait();
        void InnerScenario()
        {
            var options = new ConfigurationOptions();
            options.EndPoints.Add($"{Guid.NewGuid():N}.redis.cache.windows.net");
            options.Ssl = true;
            options.Password = "";
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 1000;
            options.BacklogPolicy = BacklogPolicy.FailFast;
            var outer = Assert.Throws<RedisConnectionException>(() =>
            {
                using var conn = ConnectionMultiplexer.Connect(options);

                conn.GetDatabase().Ping();
            });
            outer.FailureType.Should().Be(ConnectionFailureType.UnableToResolvePhysicalConnection);

            Assert.NotNull(outer.InnerException);
            if (outer.InnerException is RedisConnectionException rce)
            {
                rce.FailureType.Should().Be(ConnectionFailureType.UnableToConnect);
            }
            else if (outer.InnerException is AggregateException ae
                && ae.InnerExceptions.Any(e => e is RedisConnectionException rce2
                && rce2.FailureType == ConnectionFailureType.UnableToConnect))
            {
                // fine; at least *one* of them is the one we were hoping to see
            }
            else
            {
                Log(outer.InnerException.ToString());
                if (outer.InnerException is AggregateException inner)
                {
                    foreach (var ex in inner.InnerExceptions)
                    {
                        Log(ex.ToString());
                    }
                }
                true.Should().BeFalse(); // force fail
            }
        }
    }

    [Fact]
    public async Task abort_on_connect_fail_false_connect_timeout_error()
    {
        await RunBlockingSynchronousWithExtraThreadAsync(InnerScenario).ForAwait();
        void InnerScenario()
        {
            Skip.IfNoConfig(nameof(TestConfig.Config.AzureCacheServer), TestConfig.Current.AzureCacheServer);
            Skip.IfNoConfig(nameof(TestConfig.Config.AzureCachePassword), TestConfig.Current.AzureCachePassword);

            var options = new ConfigurationOptions();
            options.EndPoints.Add(TestConfig.Current.AzureCacheServer);
            options.Ssl = true;
            options.ConnectTimeout = 0;
            options.Password = TestConfig.Current.AzureCachePassword;

            using var conn = ConnectionMultiplexer.Connect(options);

            var ex = Assert.Throws<RedisConnectionException>(() => conn.GetDatabase().Ping());
            ex.Message.Should().Contain("ConnectTimeout");
        }
    }

    [Fact]
    public void try_get_azure_role_instance_id_no_throw() => DefaultOptionsProvider.TryGetAzureRoleInstanceIdNoThrow().Should().BeNull();

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task check_failure_recovered()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        var options = ConfigurationOptions.Parse(GetConfiguration());
        ConfigureFailureRecovery(options);

        await using var conn = await ConnectionMultiplexer.ConnectAsync(options, Writer);
        await CheckFailureRecoveredAsync(conn);
    }

    [Fact]
    [Trait(TestCategories.Category, TestCategories.SimulatedConnectionFailure)]
    public async Task check_failure_recovered_in_process()
    {
        using var server = new InProcessTestServer(Output);
        var options = server.GetClientConfig();
        ConfigureFailureRecovery(options);

        await using var conn = await ConnectionMultiplexer.ConnectAsync(options, Writer);
        await CheckFailureRecoveredAsync(conn);
    }

    private static void ConfigureFailureRecovery(ConfigurationOptions options)
    {
        options.ConnectTimeout = 10000;
        options.AllowAdmin = true;
        options.AllowSimulateConnectionFailure = true;
        options.Protocol = TestContext.Current.GetProtocol();
        options.KeepAlive = 1;
        options.HeartbeatInterval = TimeSpan.FromSeconds(1);
        options.ReconnectRetryPolicy = new LinearRetry((int)options.HeartbeatInterval.TotalMilliseconds);
    }

    private async Task CheckFailureRecoveredAsync(ConnectionMultiplexer conn)
    {
        try
        {
            await RunBlockingSynchronousWithExtraThreadAsync(InnerScenario).ForAwait();
            void InnerScenario()
            {
                conn.GetDatabase();
                var server = conn.GetServer(conn.GetEndPoints()[0]);
                Assert.SkipUnless(server.CanSimulateConnectionFailure(), "Skipping because server cannot simulate connection failure");

                conn.AllowConnect = false;

                server.SimulateConnectionFailure(SimulatedFailureType.All);

                var lastFailure = ((RedisConnectionException?)conn.GetServerSnapshot()[0].LastException)!.FailureType;
                // Depending on heartbeat races, the last exception will be a socket failure or an internal (follow-up) failure
                (new[] { ConnectionFailureType.SocketFailure, ConnectionFailureType.InternalFailure }).Should().Contain(lastFailure);

                // should reconnect within 1 keepalive interval
                conn.AllowConnect = true;
            }
            var recoveryTime = await UntilConditionAsync(TimeSpan.FromSeconds(10), () => conn.GetServerSnapshot()[0].LastException is null).ForAwait();
            Log("Connection failure recovered after {0:N0}ms", recoveryTime.TotalMilliseconds);

            conn.GetServerSnapshot()[0].LastException.Should().BeNull();
        }
        finally
        {
            ClearAmbientFailures();
        }
    }
}

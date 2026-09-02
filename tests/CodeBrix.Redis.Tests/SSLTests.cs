using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using CodeBrix.Redis.Tests.Helpers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class SSLTests(ITestOutputHelper output, SSLTests.SSLServerFixture fixture) : TestBase(output), IClassFixture<SSLTests.SSLServerFixture>
{
    //SslProtocols.Ssl2 and .Ssl3 are [Obsolete] and an attribute ARGUMENT cannot be opted in the way
    //a method body can (marking the method [Obsolete] does not cover its own attributes), so the two
    //deprecated protocols are named by their values - SslProtocols.Ssl2 == 12, .Ssl3 == 48 - and these
    //constants say which is which. The rows that use them assert that connecting with a deprecated
    //protocol FAILS, so the value is precisely what is under test.
    private const SslProtocols Ssl2Value = (SslProtocols)12;
    private const SslProtocols Ssl3Value = (SslProtocols)48;

    private SSLServerFixture Fixture { get; } = fixture;

    [Theory] // (note the 6379 port is closed)
    [InlineData(null, true)] // auto-infer port (but specify 6380)
    [InlineData(6380, true)] // all explicit
    public async Task connect_to_azure(int? port, bool ssl)
    {
        Skip.IfNoConfig(nameof(TestConfig.Config.AzureCacheServer), TestConfig.Current.AzureCacheServer);
        Skip.IfNoConfig(nameof(TestConfig.Config.AzureCachePassword), TestConfig.Current.AzureCachePassword);

        var options = new ConfigurationOptions();
        options.CertificateValidation += ShowCertFailures(Writer);
        if (port == null)
        {
            options.EndPoints.Add(TestConfig.Current.AzureCacheServer);
        }
        else
        {
            options.EndPoints.Add(TestConfig.Current.AzureCacheServer, port.Value);
        }
        options.Ssl = ssl;
        options.Password = TestConfig.Current.AzureCachePassword;
        Log(options.ToString());
        using (var connection = ConnectionMultiplexer.Connect(options))
        {
            var ttl = await connection.GetDatabase().PingAsync();
            Log(ttl.ToString());
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task connect_to_ssl_server(bool useSsl, bool specifyHost)
    {
        Fixture.SkipIfNoServer();

        var server = TestConfig.Current.SslServer;
        int? port = TestConfig.Current.SslPort;
        string? password = "";
        bool isAzure = false;
        if (string.IsNullOrWhiteSpace(server) && useSsl)
        {
            // we can bounce it past azure instead?
            server = TestConfig.Current.AzureCacheServer;
            password = TestConfig.Current.AzureCachePassword;
            port = null;
            isAzure = true;
        }
        Skip.IfNoConfig(nameof(TestConfig.Config.SslServer), server);

        var config = new ConfigurationOptions
        {
            AllowAdmin = true,
            SyncTimeout = Debugger.IsAttached ? int.MaxValue : 2000,
            Password = password,
        };
        var map = new Dictionary<string, string?>
        {
            ["config"] = null, // don't rely on config working
        };
        if (!isAzure) map["cluster"] = null;
        config.CommandMap = CommandMap.Create(map);
        if (port != null) config.EndPoints.Add(server, port.Value);
        else config.EndPoints.Add(server);

        if (useSsl)
        {
            config.Ssl = useSsl;
            if (specifyHost)
            {
                config.SslHost = server;
            }
            config.CertificateValidation += (sender, cert, chain, errors) =>
            {
                Log("errors: " + errors);
                Log("cert issued to: " + cert?.Subject);
                return true; // fingers in ears, pretend we don't know this is wrong
            };
        }

        var configString = config.ToString();
        Log("config: " + configString);
        var clone = ConfigurationOptions.Parse(configString);
        clone.ToString().Should().Be(configString);

        var log = new StringBuilder();
        Writer.EchoTo(log);

        if (useSsl)
        {
            await using var conn = await ConnectionMultiplexer.ConnectAsync(config, Writer);

            Log("Connect log:");
            lock (log)
            {
                Log(log.ToString());
            }
            Log("====");
            conn.ConnectionFailed += OnConnectionFailed;
            conn.InternalError += OnInternalError;
            var db = conn.GetDatabase();
            await db.PingAsync().ForAwait();
            using (var file = File.Create("ssl-" + useSsl + "-" + specifyHost + ".zip"))
            {
                conn.ExportConfiguration(file);
            }
            RedisKey key = Me();

            const int AsyncLoop = 2000;
            // perf; async
            await db.KeyDeleteAsync(key).ForAwait();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < AsyncLoop; i++)
            {
                try
                {
                    await db.StringIncrementAsync(key, flags: CommandFlags.FireAndForget).ForAwait();
                }
                catch (Exception ex)
                {
                    Log($"Failure on i={i}: {ex.Message}");
                    throw;
                }
            }
            // need to do this inside the timer to measure the TTLB
            long value = (long)await db.StringGetAsync(key).ForAwait();
            watch.Stop();
            value.Should().Be(AsyncLoop);
            Log($"F&F: {AsyncLoop} INCR, {watch.ElapsedMilliseconds:###,##0}ms, {(long)(AsyncLoop / watch.Elapsed.TotalSeconds)} ops/s; final value: {value}");

            // perf: sync/multi-threaded
            // TestConcurrent(db, key, 30, 10);
            // TestConcurrent(db, key, 30, 20);
            // TestConcurrent(db, key, 30, 30);
            // TestConcurrent(db, key, 30, 40);
            // TestConcurrent(db, key, 30, 50);
        }
        else
        {
            Assert.Throws<RedisConnectionException>(() => ConnectionMultiplexer.Connect(config, Writer));
        }
    }

    // Docker configured with only TLS_AES_256_GCM_SHA384 for testing
    [Theory]
    [InlineData(SslProtocols.None, true, TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, TlsCipherSuite.TLS_AES_256_GCM_SHA384)]
    [InlineData(SslProtocols.Tls12, true, TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, TlsCipherSuite.TLS_AES_256_GCM_SHA384)]
    [InlineData(SslProtocols.Tls13, true, TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, TlsCipherSuite.TLS_AES_256_GCM_SHA384)]
    [InlineData(SslProtocols.Tls12, false, TlsCipherSuite.TLS_AES_128_CCM_8_SHA256)]
    [InlineData(SslProtocols.Tls12, true)]
    [InlineData(SslProtocols.Tls13, true)]
    [InlineData(Ssl2Value, false)]
    [InlineData(Ssl3Value, false)]
    [InlineData(SslProtocols.Tls12 | SslProtocols.Tls13, true)]
    [InlineData(Ssl3Value | SslProtocols.Tls12 | SslProtocols.Tls13, true)]
    [InlineData(Ssl2Value, false, TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, TlsCipherSuite.TLS_AES_256_GCM_SHA384)]
    public async Task connect_ssl_client_authentication_options(SslProtocols protocols, bool expectSuccess, params TlsCipherSuite[] tlsCipherSuites)
    {
        Fixture.SkipIfNoServer();
        //CipherSuitesPolicy throws PlatformNotSupportedException on Windows, so the rows that carry
        //cipher suites cannot run there; the same OperatingSystem check guards the construction below,
        //which is what CA1416 asks for (the analyzer cannot see this one from inside the lambda).
        Assert.SkipWhen(tlsCipherSuites.Length > 0 && OperatingSystem.IsWindows(), "CipherSuitesPolicy is not supported on Windows");

        try
        {
            var config = new ConfigurationOptions()
            {
                EndPoints = { TestConfig.Current.SslServerAndPort },
                AllowAdmin = true,
                ConnectRetry = 1,
                SyncTimeout = Debugger.IsAttached ? int.MaxValue : 5000,
                Ssl = true,
                SslClientAuthenticationOptions = host => new SslClientAuthenticationOptions()
                {
                    TargetHost = host,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    EnabledSslProtocols = protocols,
                    CipherSuitesPolicy = tlsCipherSuites?.Length > 0 && !OperatingSystem.IsWindows() ? new CipherSuitesPolicy(tlsCipherSuites) : null,
                    RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
                    {
                        Log("  Errors: " + errors);
                        Log("  Cert issued to: " + cert?.Subject);
                        return true;
                    },
                },
            };

            if (expectSuccess)
            {
                await using var conn = await ConnectionMultiplexer.ConnectAsync(config, Writer);

                var db = conn.GetDatabase();
                Log("Pinging...");
                var time = await db.PingAsync().ForAwait();
                Log($"Ping time: {time}");
            }
            else
            {
                var ex = await Assert.ThrowsAsync<RedisConnectionException>(() => ConnectionMultiplexer.ConnectAsync(config, Writer));
                Log("(Expected) Failure connecting: " + ex.Message);
                if (ex.InnerException is PlatformNotSupportedException pnse)
                {
                    Assert.Skip("Expected failure, but also test not supported on this platform: " + pnse.Message);
                }
            }
        }
        catch (RedisException ex) when (ex.InnerException is PlatformNotSupportedException pnse)
        {
            Assert.Skip("Test not supported on this platform: " + pnse.Message);
        }
    }

    [Fact]
    public async Task redis_labs_ssl()
    {
        Skip.IfNoConfig(nameof(TestConfig.Config.RedisLabsSslServer), TestConfig.Current.RedisLabsSslServer);
        Skip.IfNoConfig(nameof(TestConfig.Config.RedisLabsPfxPath), TestConfig.Current.RedisLabsPfxPath);

        //was previously: new X509Certificate2(path, "") - obsolete (SYSLIB0057); a .pfx path with a
        //password is exactly LoadPkcs12FromFile.
        var cert = X509CertificateLoader.LoadPkcs12FromFile(TestConfig.Current.RedisLabsPfxPath, "");
        cert.Should().NotBeNull();
        Log("Thumbprint: " + cert.Thumbprint);

        int timeout = 5000;
        if (Debugger.IsAttached) timeout *= 100;
        var options = new ConfigurationOptions
        {
            EndPoints = { { TestConfig.Current.RedisLabsSslServer, TestConfig.Current.RedisLabsSslPort } },
            ConnectTimeout = timeout,
            AllowAdmin = true,
            CommandMap = CommandMap.Create(
                new HashSet<string>
                {
                    "subscribe",
                    "unsubscribe",
                    "cluster",
                },
                false),
        };

        options.TrustIssuer("redislabs_ca.pem");

        if (!Directory.Exists(Me())) Directory.CreateDirectory(Me());
        options.Ssl = true;
        options.CertificateSelection += (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => cert;

        await using var conn = ConnectionMultiplexer.Connect(options);

        RedisKey key = Me();
        var db = conn.GetDatabase();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        string? s = db.StringGet(key);
        s.Should().BeNull();
        db.StringSet(key, "abc", flags: CommandFlags.FireAndForget);
        s = db.StringGet(key);
        s.Should().Be("abc");

        var latency = await db.PingAsync();
        Log("RedisLabs latency: {0:###,##0.##}ms", latency.TotalMilliseconds);

        using (var file = File.Create("RedisLabs.zip"))
        {
            conn.ExportConfiguration(file);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task redis_labs_environment_variable_client_certificate(bool setEnv)
    {
        try
        {
            Skip.IfNoConfig(nameof(TestConfig.Config.RedisLabsSslServer), TestConfig.Current.RedisLabsSslServer);
            Skip.IfNoConfig(nameof(TestConfig.Config.RedisLabsPfxPath), TestConfig.Current.RedisLabsPfxPath);

            if (setEnv)
            {
                Environment.SetEnvironmentVariable("SERedis_ClientCertPfxPath", TestConfig.Current.RedisLabsPfxPath);
                Environment.SetEnvironmentVariable("SERedis_IssuerCertPath", "redislabs_ca.pem");
                // check env worked
                Environment.GetEnvironmentVariable("SERedis_ClientCertPfxPath").Should().Be(TestConfig.Current.RedisLabsPfxPath);
                Environment.GetEnvironmentVariable("SERedis_IssuerCertPath").Should().Be("redislabs_ca.pem");
            }
            int timeout = 5000;
            if (Debugger.IsAttached) timeout *= 100;
            var options = new ConfigurationOptions
            {
                EndPoints = { { TestConfig.Current.RedisLabsSslServer, TestConfig.Current.RedisLabsSslPort } },
                ConnectTimeout = timeout,
                AllowAdmin = true,
                CommandMap = CommandMap.Create(
                    new HashSet<string>
                    {
                        "subscribe",
                        "unsubscribe",
                        "cluster",
                    },
                    false),
            };

            if (!Directory.Exists(Me())) Directory.CreateDirectory(Me());
            options.Ssl = true;

            await using var conn = ConnectionMultiplexer.Connect(options);

            RedisKey key = Me();
            if (!setEnv) Assert.Fail("Could not set environment");

            var db = conn.GetDatabase();
            db.KeyDelete(key, CommandFlags.FireAndForget);
            string? s = db.StringGet(key);
            s.Should().BeNull();
            db.StringSet(key, "abc");
            s = db.StringGet(key);
            s.Should().Be("abc");

            var latency = await db.PingAsync();
            Log("RedisLabs latency: {0:###,##0.##}ms", latency.TotalMilliseconds);

            using (var file = File.Create("RedisLabs.zip"))
            {
                conn.ExportConfiguration(file);
            }
        }
        catch (RedisConnectionException ex) when (!setEnv && ex.FailureType == ConnectionFailureType.UnableToConnect)
        {
        }
        finally
        {
            Environment.SetEnvironmentVariable("SERedis_ClientCertPfxPath", null);
        }
    }

    [Fact]
    public void ssl_host_inferred_from_endpoints()
    {
        var options = new ConfigurationOptions
        {
            EndPoints =
            {
                { "mycache.rediscache.windows.net", 15000 },
                { "mycache.rediscache.windows.net", 15001 },
                { "mycache.rediscache.windows.net", 15002 },
            },
            Ssl = true,
        };
        options.SslHost.Should().Be("mycache.rediscache.windows.net");
        options = new ConfigurationOptions()
        {
            EndPoints = { { "121.23.23.45", 15000 } },
        };
        options.SslHost.Should().BeNull();
    }

    private void Check(string name, object? x, object? y)
    {
        Log($"{name}: {(x == null ? "(null)" : x.ToString())} vs {(y == null ? "(null)" : y.ToString())}");
        //kept as the xUnit form: these are untyped object values and some of them are collections
        //(EndPoints), which Assert.Equal compares element by element. ObjectAssertions.Be falls
        //through to Equals, which for EndPointCollection is reference equality.
        Assert.Equal(x, y);
    }

    [Fact]
    public void issue883_exhaustive()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            var fields = typeof(ConfigurationOptions).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var all = CultureInfo.GetCultures(CultureTypes.AllCultures);
            Log($"Checking {all.Length} cultures...");
            foreach (var ci in all)
            {
                Log("Testing: " + ci.Name);
                CultureInfo.CurrentCulture = ci;

                var a = ConfigurationOptions.Parse("myDNS:883,password=mypassword,connectRetry=3,connectTimeout=5000,syncTimeout=5000,defaultDatabase=0,ssl=true,abortConnect=false");
                var b = new ConfigurationOptions
                {
                    EndPoints = { { "myDNS", 883 } },
                    Password = "mypassword",
                    ConnectRetry = 3,
                    ConnectTimeout = 5000,
                    SyncTimeout = 5000,
                    DefaultDatabase = 0,
                    Ssl = true,
                    AbortOnConnectFail = false,
                };
                _ = a.Defaults;
                _ = b.Defaults; // ensure the lazily materialized provider matches the parsed shape
                Log($"computed: {b.ToString(true)}");

                Log("Checking endpoints...");
                var c = a.EndPoints.Cast<DnsEndPoint>().Single();
                var d = b.EndPoints.Cast<DnsEndPoint>().Single();
                Check(nameof(c.Host), c.Host, d.Host);
                Check(nameof(c.Port), c.Port, d.Port);
                Check(nameof(c.AddressFamily), c.AddressFamily, d.AddressFamily);

                Log($"Comparing {fields.Length} fields...");
                Array.Sort(fields, (x, y) => string.CompareOrdinal(x.Name, y.Name));
                foreach (var field in fields)
                {
                    if (field.Name == "defaultOptions")
                    {
                        var x = field.GetValue(a);
                        var y = field.GetValue(b);
                        Log($"{field.Name}: {(x == null ? "(null)" : x.GetType().Name)} vs {(y == null ? "(null)" : y.GetType().Name)}");
                        Check(field.Name + ".Type", x?.GetType(), y?.GetType());
                        continue;
                    }
                    Check(field.Name, field.GetValue(a), field.GetValue(b));
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = old;
        }
    }

    [Fact]
    public async Task ssl_parse_via_config_issue883_config_object()
    {
        Skip.IfNoConfig(nameof(TestConfig.Config.AzureCacheServer), TestConfig.Current.AzureCacheServer);
        Skip.IfNoConfig(nameof(TestConfig.Config.AzureCachePassword), TestConfig.Current.AzureCachePassword);

        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            Ssl = true,
            ConnectRetry = 3,
            ConnectTimeout = 5000,
            SyncTimeout = 5000,
            DefaultDatabase = 0,
            EndPoints = { { TestConfig.Current.AzureCacheServer, 6380 } },
            Password = TestConfig.Current.AzureCachePassword,
        };
        options.CertificateValidation += ShowCertFailures(Writer);

        await using var conn = ConnectionMultiplexer.Connect(options);

        await conn.GetDatabase().PingAsync();
    }

    public static RemoteCertificateValidationCallback? ShowCertFailures(TextWriterOutputHelper output)
    {
        if (output == null)
        {
            return null;
        }

        return (sender, certificate, chain, sslPolicyErrors) =>
        {
            void WriteStatus(X509ChainStatus[] status)
            {
                if (status != null)
                {
                    for (int i = 0; i < status.Length; i++)
                    {
                        var item = status[i];
                        Log(output, $"\tstatus {i}: {item.Status}, {item.StatusInformation}");
                    }
                }
            }
            lock (output)
            {
                if (certificate != null)
                {
                    Log(output, $"Subject: {certificate.Subject}");
                }
                Log(output, $"Policy errors: {sslPolicyErrors}");
                if (chain != null)
                {
                    WriteStatus(chain.ChainStatus);

                    var elements = chain.ChainElements;
                    if (elements != null)
                    {
                        int index = 0;
                        foreach (var item in elements)
                        {
                            Log(output, $"{index++}: {item.Certificate.Subject}; {item.Information}");
                            WriteStatus(item.ChainElementStatus);
                        }
                    }
                }
            }
            return sslPolicyErrors == SslPolicyErrors.None;
        };
    }

    [Fact]
    public async Task ssl_parse_via_config_issue883_config_string()
    {
        Skip.IfNoConfig(nameof(TestConfig.Config.AzureCacheServer), TestConfig.Current.AzureCacheServer);
        Skip.IfNoConfig(nameof(TestConfig.Config.AzureCachePassword), TestConfig.Current.AzureCachePassword);

        var configString = $"{TestConfig.Current.AzureCacheServer}:6380,password={TestConfig.Current.AzureCachePassword},connectRetry=3,connectTimeout=5000,syncTimeout=5000,defaultDatabase=0,ssl=true,abortConnect=false";
        var options = ConfigurationOptions.Parse(configString);
        options.CertificateValidation += ShowCertFailures(Writer);

        await using var conn = ConnectionMultiplexer.Connect(options);

        await conn.GetDatabase().PingAsync();
    }

    [Fact]
    public void config_object_issue1407_to_string_includes_ssl_protocols()
    {
        const SslProtocols sslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        var sourceOptions = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            Ssl = true,
            SslProtocols = sslProtocols,
            ConnectRetry = 3,
            ConnectTimeout = 5000,
            SyncTimeout = 5000,
            DefaultDatabase = 0,
            EndPoints = { { "endpoint.test", 6380 } },
            Password = "123456",
        };

        var targetOptions = ConfigurationOptions.Parse(sourceOptions.ToString());
        targetOptions.SslProtocols.Should().Be(sourceOptions.SslProtocols);
    }

    public class SSLServerFixture : IDisposable
    {
        public bool ServerRunning { get; }

        public SSLServerFixture()
        {
            //with the container tier off there is deliberately nothing to probe; do not open a socket
            ServerRunning = TestConfig.ServersAvailable
                            && TestConfig.IsServerRunning(TestConfig.Current.SslServer, TestConfig.Current.SslPort);
        }

        public void SkipIfNoServer()
        {
            //ask the tier gate first, so a tier-off run reports the tier reason rather than "not running"
            Skip.IfNoContainers();
            Skip.IfNoConfig(nameof(TestConfig.Config.SslServer), TestConfig.Current.SslServer);
            if (!ServerRunning)
            {
                Assert.Skip($"SSL/TLS Server was not running at {TestConfig.Current.SslServer}:{TestConfig.Current.SslPort}");
            }
        }

        public void Dispose() { }
    }
}

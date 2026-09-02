using System;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Redis.Configuration;
using CodeBrix.Redis.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

[RunPerProtocol]
public class ConfigTests(ITestOutputHelper output, SharedConnectionFixture fixture) : TestBase(output, fixture)
{
    //SslProtocols.Ssl3 is [Obsolete] and an attribute ARGUMENT cannot be opted in the way a method
    //body can (marking the method [Obsolete] does not cover its own attributes), so the deprecated
    //protocol is named by its value - SslProtocols.Ssl3 == 48 - and this constant says which it is.
    //Parsing "sslProtocols=Ssl3" is what that InlineData row checks, and the value is what it checks with.
    private const SslProtocols Ssl3Value = (SslProtocols)48;

    private static Version BaseDefaultVersion => DefaultOptionsProvider.BaseDefaultVersion;

    private static void ApplyTestDefaults(ConfigurationOptions options, bool applyProtocol = true)
    {
        if (applyProtocol) options.Protocol = TestContext.Current.GetProtocol();
    }

    private static string RemoveTestDefaults(string configurationString)
    {
        var pattern = TestContext.Current.GetProtocol() switch
        {
            RedisProtocol.Resp2 => ",protocol=resp2(?=,|$)",
            RedisProtocol.Resp3 => ",protocol=resp3(?=,|$)",
            _ => null,
        };
        return pattern is null
            ? configurationString
            : Regex.Replace(configurationString, pattern, "");
    }
    private static ConfigurationOptions Parse(string configuration, bool applyProtocol = true)
    {
        var options = ConfigurationOptions.Parse(configuration);
        ApplyTestDefaults(options, applyProtocol);
        return options;
    }

    [Fact]
    public void expected_fields()
    {
        // if this test fails, check that you've updated ConfigurationOptions.Clone(), then: fix the test!
        // this is a simple but pragmatic "have you considered?" check
        var fields = (
            from field in typeof(ConfigurationOptions).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            let name = Regex.Replace(field.Name, """^<(\w+)>k__BackingField$""", "$1")
            where name is not "WriteMode" // silently ignored
            orderby name
            select name).ToArray();
        fields.Should().Equal(new[]
            {
                "_protocol",
                "asyncTimeout",
                "backlogPolicy",
                "BeforeSocketConnect",
                "CertificateSelection",
                "CertificateValidation",
                "ChannelPrefix",
                "CircuitBreaker",
                "ClientName",
                "commandMap",
                "configChannel",
                "configCheckSeconds",
                "connectRetry",
                "connectTimeout",
                "defaultDatabase",
                "defaultOptions",
                "defaultVersion",
                "EndPoints",
                "heartbeatInterval",
                "keepAlive",
                "LibraryName",
                "loggerFactory",
                "optionFlags",
#if DEBUG
                "OutputLog",
#endif
                "password",
                "proxy",
                "reconnectRetryPolicy",
                "RequestBufferPool",
                "ResponseBufferPool",
                "responseTimeout",
                "RetryPolicy",
                "sentinelPassword",
                "sentinelUser",
                "ServiceName",
                //was previously: "SocketManager" (the auto-property backing field). Phase 3 gave the
                //[Obsolete] SocketManager property a plain private field so Clone()/Reset() could
                //reach it without calling an obsolete member; the field name changed with it.
                "socketManager",
                "SslClientAuthenticationOptions",
                "sslHost",
                "sslProtocols",
                "syncTimeout",
                "tieBreaker",
                "Tunnel",
                "user",
            });
    }

    [Fact]
    public void option_keys_are_all_normalized()
    {
        var optionKeys = typeof(ConfigurationOptions).GetNestedType("OptionKeys", BindingFlags.NonPublic)!;
        var constants = (
            from field in optionKeys.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            where field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string)
            orderby field.Name
            select (string)field.GetRawConstantValue()!).ToArray();

        var normalizedOptions = (System.Collections.Generic.IReadOnlyDictionary<string, string>)optionKeys
            .GetField("normalizedOptions", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        normalizedOptions.Keys.OrderBy(x => x, StringComparer.Ordinal).Should().Equal(constants.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void ssl_protocols_single_value()
    {
        var options = Parse("myhost,sslProtocols=Tls12");
        options.SslProtocols.GetValueOrDefault().Should().Be(SslProtocols.Tls12);
    }

    [Fact]
    public void ssl_protocols_multiple_values()
    {
        var options = Parse("myhost,sslProtocols=Tls12|Tls13");
        options.SslProtocols.GetValueOrDefault().Should().Be(SslProtocols.Tls12 | SslProtocols.Tls13);
    }

    [Theory]
    [InlineData("checkCertificateRevocation=false", false)]
    [InlineData("checkCertificateRevocation=true", true)]
    [InlineData("", true)]
    public void configuration_option_check_certificate_revocation(string conString, bool expectedValue)
    {
        var options = Parse($"host,{conString}");
        options.CheckCertificateRevocation.Should().Be(expectedValue);
        var toString = options.ToString();
        Assert.Contains(conString, toString, StringComparison.CurrentCultureIgnoreCase);
    }

    [Fact]
    public void ssl_protocols_using_integer_value()
    {
        // The below scenario is for cases where the *targeted*
        // .NET framework version (e.g. .NET 4.0) doesn't define an enum value (e.g. Tls11)
        // but the OS has been patched with support
        const int integerValue = (int)(SslProtocols.Tls12 | SslProtocols.Tls13);
        var options = Parse("myhost,sslProtocols=" + integerValue);
        options.SslProtocols.GetValueOrDefault().Should().Be(SslProtocols.Tls12 | SslProtocols.Tls13);
    }

    [Fact]
    public void ssl_protocols_invalid_value()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Parse("myhost,sslProtocols=InvalidSslProtocol"));
    }

    [Theory]
    [InlineData("contoso.redis.cache.windows.net:6380", true)]
    [InlineData("contoso.REDIS.CACHE.chinacloudapi.cn:6380", true)] // added a few upper case chars to validate comparison
    [InlineData("contoso.redis.cache.usgovcloudapi.net:6380", true)]
    [InlineData("contoso.redis.cache.sovcloud-api.de:6380", true)]
    [InlineData("contoso.redis.cache.sovcloud-api.fr:6380", true)]
    [InlineData("contoso.redis.cache.windows.net:6379", false)] // non-SSL port
    [InlineData("contoso.redis.cache.windows.net:10000", false)] // wrong port
    [InlineData("contoso.redis.cache.windows.net", false)] // no port
    public void configuration_options_default_for_azure(string hostAndPort, bool sslShouldBeEnabled)
    {
        Version defaultAzureVersion = new(6, 0, 0);
        var options = Parse(hostAndPort);
        options.DefaultVersion.Equals(defaultAzureVersion).Should().BeTrue();
        options.AbortOnConnectFail.Should().BeFalse();
        options.Ssl.Should().Be(sslShouldBeEnabled);
    }

    [Theory]
    [InlineData("contoso.redis.azure.net:10000", true)]
    [InlineData("contoso.redis.chinacloudapi.cn:10000", true)]
    [InlineData("contoso.redis.usgovcloudapi.net:10000", true)]
    [InlineData("contoso.redisenterprise.cache.azure.net:10000", true)]
    [InlineData("contoso.REDIS.sovcloud-api.de:10000", true)] // added a few upper case chars to validate comparison
    [InlineData("contoso.redis.sovcloud-api.fr:10000", true)]
    [InlineData("contoso.redis.azure.net:6379", true)] // AMR port is usually 10000, assume SSL regardless
    [InlineData("contoso.redis.azure.net:6380", true)] // AMR port is usually 10000, assume SSL regardless
    [InlineData("contoso.redis.azure.net", true)] // no port, assume SSL
    public void configuration_options_default_for_azure_managed_redis(string hostAndPort, bool sslShouldBeEnabled)
    {
        Version defaultAzureManagedRedisVersion = new(7, 4, 0);
        var options = Parse(hostAndPort);
        options.DefaultVersion.Equals(defaultAzureManagedRedisVersion).Should().BeTrue();
        options.AbortOnConnectFail.Should().BeFalse();
        options.Ssl.Should().Be(sslShouldBeEnabled);
    }

    [Theory]
    // azure managed redis, no overrides
    [InlineData("contoso.redis.azure.net:10000", RedisProtocol.Resp3, true)] // default
    [InlineData("contoso.redis.azure.net:10000,protocol=resp2", RedisProtocol.Resp2, false)] // opt-out
    [InlineData("contoso.redis.azure.net:10000,protocol=resp3", RedisProtocol.Resp3, true)] // opt-in
    [InlineData("contoso.redis.azure.net:10000,version=5", RedisProtocol.Resp3, true)] // low version *ignored* (provider wins)
    // azure redis cache, no overrides (we expect this to change in v3)
    [InlineData("contoso.redis.cache.windows.net:6380", null, true)] // default
    [InlineData("contoso.redis.cache.windows.net:6380,protocol=resp2", RedisProtocol.Resp2, false)] // opt-out
    [InlineData("contoso.redis.cache.windows.net:6380,protocol=resp3", RedisProtocol.Resp3, true)] // opt-in
    [InlineData("contoso.redis.cache.windows.net:6380,version=5", null, false)] // low version means resp2
    // arbitrary endpoint (we expect this to change in v3)
    [InlineData("myserver:6379", null, true)] // default
    [InlineData("myserver:6379,protocol=resp2", RedisProtocol.Resp2, false)] // opt-out
    [InlineData("myserver:6379,protocol=resp3", RedisProtocol.Resp3, true)] // opt-in
    [InlineData("myserver:6379,version=5", null, false)] // low version means resp2
    public void CorrectRespProtocol(string config, RedisProtocol? expected, bool useResp3)
    {
        var options = Parse(config, applyProtocol: false);
        options.Protocol.Should().Be(expected);
        options.TryResp3().Should().Be(useResp3);
    }

    [Fact]
    public void configuration_options_for_azure_when_specified()
    {
        var options = Parse("contoso.redis.cache.windows.net,abortConnect=true, version=2.1.1");
        options.DefaultVersion.Equals(new Version(2, 1, 1)).Should().BeTrue();
        options.AbortOnConnectFail.Should().BeTrue();
    }

    [Theory]
    [InlineData("redis.contoso.com")] // no port
    [InlineData("redis.contoso.com:xx")] // invalid port
    [InlineData("redis.contoso.com:6379")] // valid port
    [InlineData("contoso.Xredis.cache.windows.net:6380")] // almost an Azure Cache for Redis host name
    [InlineData("contoso.redis.cache.windows.netX:6380")] // almost an Azure Cache for Redis host name
    [InlineData("contoso.redis.cache.windows.net.X:6380")] // almost an Azure Cache for Redis host name
    [InlineData("contoso.Xredis.azure.net:10000")] // almost an Azure Managed Redis host name
    [InlineData("contoso.redis.azure.netX:10000")] // almost an Azure Managed Redis host name
    [InlineData("contoso.redis.azure.net.X:10000")] // almost an Azure Managed Redis host name
    [InlineData("contoso.redis.cache.windows.net:xx")] // Azure Cache for Redis host name with invalid port
    [InlineData("contoso.redis.cache.windows.net:")] // Azure Cache for Redis host name with missing port
    [InlineData("contoso.redis.azure.net:xx")] // AMR host name with invalid port
    [InlineData("contoso.redis.azure.net:")] // AMR host name with missing port
    public void ConfigurationOptionsDefaultForNonAzure(string hostAndPort)
    {
        var options = Parse(hostAndPort);
        options.DefaultVersion.Equals(BaseDefaultVersion).Should().BeTrue();
        options.AbortOnConnectFail.Should().BeTrue();
        options.Ssl.Should().BeFalse();
    }

    [Fact]
    public void configuration_options_default_when_no_endpoints_specified_yet()
    {
        var options = new ConfigurationOptions();
        options.DefaultVersion.Equals(BaseDefaultVersion).Should().BeTrue();
        options.AbortOnConnectFail.Should().BeTrue();
    }

    [Fact]
    public void configuration_options_sync_timeout()
    {
        // Default check
        var options = new ConfigurationOptions();
        options.SyncTimeout.Should().Be(5000);

        options = Parse("syncTimeout=20");
        options.SyncTimeout.Should().Be(20);
    }

    [Theory]
    [InlineData("127.1:6379", AddressFamily.InterNetwork, "127.0.0.1", 6379)]
    [InlineData("127.0.0.1:6379", AddressFamily.InterNetwork, "127.0.0.1", 6379)]
    [InlineData("2a01:9820:1:24::1:1:6379", AddressFamily.InterNetworkV6, "2a01:9820:1:24:0:1:1:6379", 0)]
    [InlineData("[2a01:9820:1:24::1:1]:6379", AddressFamily.InterNetworkV6, "2a01:9820:1:24::1:1", 6379)]
    public void configuration_options_i_pv_6_parsing(string configString, AddressFamily family, string address, int port)
    {
        var options = Parse(configString);
        options.EndPoints.Should().ContainSingle();
        var ep = Assert.IsType<IPEndPoint>(options.EndPoints[0]);
        ep.AddressFamily.Should().Be(family);
        ep.Address.ToString().Should().Be(address);
        ep.Port.Should().Be(port);
    }

    [Fact]
    public void can_parse_and_format_unix_domain_socket()
    {
        const string ConfigString = "!/some/path,allowAdmin=True";
        var config = Parse(ConfigString);
        config.AllowAdmin.Should().BeTrue();
        var ep = Assert.IsType<UnixDomainSocketEndPoint>(Assert.Single(config.EndPoints));
        ep.ToString().Should().Be("/some/path");
        RemoveTestDefaults(config.ToString()).Should().Be(ConfigString);
    }

    [Fact]
    public async Task talk_to_nonsense_server()
    {
        var config = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            EndPoints =
            {
                { "127.0.0.1:1234" },
            },
            ConnectTimeout = 200,
        };
        ApplyTestDefaults(config);
        var log = new StringWriter();
        await using (var conn = ConnectionMultiplexer.Connect(config, log))
        {
            Log(log.ToString());
            conn.IsConnected.Should().BeFalse();
        }
    }

    [Fact]
    public async Task test_manual_heartbeat()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        var options = Parse(GetConfiguration());
        options.HeartbeatInterval = TimeSpan.FromMilliseconds(100);
        await using var conn = await ConnectionMultiplexer.ConnectAsync(options);

        foreach (var ep in conn.GetServerSnapshot().ToArray())
        {
            ep.WriteEverySeconds = 1;
        }

        var db = conn.GetDatabase();
        await db.PingAsync();

        var before = conn.OperationCount;

        Log("Sleeping to test heartbeat...");
        await UntilConditionAsync(TimeSpan.FromSeconds(5), () => conn.OperationCount > before + 1).ForAwait();
        var after = conn.OperationCount;

        (after >= before + 1).Should().BeTrue($"after: {after}, before: {before}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(200)]
    public async Task get_slowlog(int count)
    {
        await using var conn = Create(allowAdmin: true);

        var rows = GetAnyPrimary(conn).SlowlogGet(count);
        Assert.NotNull(rows);
    }

    [Fact]
    public async Task clear_slowlog()
    {
        await using var conn = Create(allowAdmin: true);

        GetAnyPrimary(conn).SlowlogReset();
    }

    [Fact]
    public async Task client_name()
    {
        await using var conn = Create(clientName: "Test Rig", allowAdmin: true, shared: false);

        conn.ClientName.Should().Be("Test Rig");

        var db = conn.GetDatabase();
        await db.PingAsync();

        var name = (string?)(await GetAnyPrimary(conn).ExecuteAsync("CLIENT", "GETNAME"));
        name.Should().Be("TestRig");
    }

    [Fact]
    public async Task client_library_name()
    {
        await using var conn = Create(allowAdmin: true, shared: false);
        var server = GetAnyPrimary(conn);

        await server.PingAsync();
        var possibleId = conn.GetConnectionId(server.EndPoint, ConnectionType.Interactive);

        if (possibleId is null)
        {
            Log("(client id not available)");
            return;
        }
        var id = possibleId.Value;
        var libName = server.ClientList().Single(x => x.Id == id).LibraryName;
        if (libName is not null) // server-version dependent
        {
            Log("library name: {0}", libName);
            libName.Should().Be("CodeBrix.Redis");

            conn.AddLibraryNameSuffix("foo");
            conn.AddLibraryNameSuffix("bar");
            conn.AddLibraryNameSuffix("foo");

            libName = (await server.ClientListAsync()).Single(x => x.Id == id).LibraryName;
            Log($"library name: {libName}");
            libName.Should().Be("CodeBrix.Redis-bar-foo");
        }
        else
        {
            Log("(library name not available)");
        }
    }

    [Fact]
    public async Task default_client_name()
    {
        await using var conn = Create(allowAdmin: true, caller: "", shared: false); // force default naming to kick in

        conn.ClientName.Should().Be($"{Environment.MachineName}(CodeBrix.Redis-v{Utils.GetLibVersion()})");
        var db = conn.GetDatabase();
        await db.PingAsync();

        var name = (string?)GetAnyPrimary(conn).Execute("CLIENT", "GETNAME");
        name.Should().Be($"{Environment.MachineName}(CodeBrix.Redis-v{Utils.GetLibVersion()})");
    }

    [Fact]
    public async Task read_config_with_config_disabled()
    {
        await using var conn = Create(allowAdmin: true, disabledCommands: ["config", "info"]);

        var server = GetAnyPrimary(conn);
        var ex = Assert.Throws<RedisCommandException>(() => server.ConfigGet());
        ex.Message.Should().Be("This operation has been disabled in the command-map and cannot be used: CONFIG");
    }

    [Fact]
    public async Task connect_with_subscribe_disabled()
    {
        await using var conn = Create(allowAdmin: true, disabledCommands: ["subscribe"]);

        conn.IsConnected.Should().BeTrue();
        var servers = conn.GetServerSnapshot();
        servers[0].IsConnected.Should().BeTrue();
        if (!TestContext.Current.IsResp3())
        {
            servers[0].IsSubscriberConnected.Should().BeFalse();
        }

        var ex = Assert.Throws<RedisCommandException>(() => conn.GetSubscriber().Subscribe(RedisChannel.Literal(Me()), (_, _) => GC.KeepAlive(this)));
        ex.Message.Should().Be("This operation has been disabled in the command-map and cannot be used: SUBSCRIBE");
    }

    [Fact]
    public async Task read_config()
    {
        await using var conn = Create(allowAdmin: true);

        Log("about to get config");
        var server = GetAnyPrimary(conn);
        var all = server.ConfigGet();
        (all.Length > 0).Should().BeTrue("any");

        var pairs = all.ToDictionary(x => (string)x.Key, x => (string)x.Value, StringComparer.InvariantCultureIgnoreCase);

        pairs.Count.Should().Be(all.Length);
        pairs.ContainsKey("timeout").Should().BeTrue("timeout");
        var val = int.Parse(pairs["timeout"]);

        pairs.ContainsKey("port").Should().BeTrue("port");
        val = int.Parse(pairs["port"]);
        val.Should().Be(TestConfig.Current.PrimaryPort);
    }

    [Fact]
    public async Task get_time()
    {
        await using var conn = Create();

        var server = GetAnyPrimary(conn);
        var serverTime = server.Time();
        var localTime = DateTime.UtcNow;
        Log("Server: " + serverTime.ToString(CultureInfo.InvariantCulture));
        Log("Local: " + localTime.ToString(CultureInfo.InvariantCulture));
        serverTime.Should().BeCloseTo(localTime, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task debug_object()
    {
        await using var conn = Create(allowAdmin: true);
        await AssertDebugCommandEnabledAsync(conn);

        var db = conn.GetDatabase();
        RedisKey key = Me();
        db.KeyDelete(key, CommandFlags.FireAndForget);
        db.StringIncrement(key, flags: CommandFlags.FireAndForget);
        var debug = (string?)db.DebugObject(key);
        Assert.NotNull(debug); //kept as the xUnit form: it carries [NotNull], so the compiler's null-state flows to the dereference below
        debug.Should().Contain("encoding:int serializedlength:2");
    }

    [Fact]
    public async Task get_info()
    {
        await using var conn = Create(allowAdmin: true);

        var server = GetAnyPrimary(conn);
        var info1 = server.Info();
        (info1.Length > 5).Should().BeTrue();
        Log("All sections");
        foreach (var group in info1)
        {
            Log(group.Key);
        }
        var first = info1[0];
        Log("Full info for: " + first.Key);
        foreach (var setting in first)
        {
            Log("  {0}  ==>  {1}", setting.Key, setting.Value);
        }

        var info2 = server.Info("cpu");
        info2.Should().ContainSingle();
        var cpu = info2.Single();
        Log("Full info for: " + cpu.Key);
        foreach (var setting in cpu)
        {
            Log("  {0}  ==>  {1}", setting.Key, setting.Value);
        }
        var cpuCount = cpu.Count();
        (cpuCount > 2).Should().BeTrue();
        if (cpu.Key != "CPU")
        {
            // seem to be seeing this in logs; add lots of detail
            var sb = new StringBuilder("Expected CPU, got ").AppendLine(cpu.Key);
            foreach (var setting in cpu)
            {
                sb.Append(setting.Key).Append('=').AppendLine(setting.Value);
            }
            Assert.Fail(sb.ToString());
        }
        cpu.Key.Should().Be("CPU");
        cpu.Should().Contain(x => x.Key == "used_cpu_sys");
        cpu.Should().Contain(x => x.Key == "used_cpu_user");
    }

    [Fact]
    public async Task get_info_raw()
    {
        await using var conn = Create(allowAdmin: true);

        var server = GetAnyPrimary(conn);
        var info = server.InfoRaw();
        info.Should().Contain("used_cpu_sys");
        info.Should().Contain("used_cpu_user");
    }

    [Fact]
    public async Task get_clients()
    {
        var name = Guid.NewGuid().ToString();
        await using var conn = Create(clientName: name, allowAdmin: true, shared: false);

        var server = GetAnyPrimary(conn);
        var clients = server.ClientList();
        (clients.Length > 0).Should().BeTrue("no clients"); // ourselves!
        clients.Any(x => x.Name == name).Should().BeTrue("expected: " + name);

        if (server.Features.ClientId)
        {
            var id = conn.GetConnectionId(server.EndPoint, ConnectionType.Interactive);
            Log("client id: " + id);
            Assert.NotNull(id);
            clients.Any(x => x.Id == id).Should().BeTrue("expected: " + id);
            id = conn.GetConnectionId(server.EndPoint, ConnectionType.Subscription);
            Assert.NotNull(id);
            clients.Any(x => x.Id == id).Should().BeTrue("expected: " + id);

            var self = clients.First(x => x.Id == id);
            if (server.Version.Major >= 7)
            {
                self.Protocol.Should().Be(TestContext.Current.GetProtocol());
            }
            else
            {
                self.Protocol.Should().BeNull();
            }
        }
    }

    [Fact]
    public async Task slow_log()
    {
        await using var conn = Create(allowAdmin: true);

        var server = GetAnyPrimary(conn);
        server.SlowlogGet();
        server.SlowlogReset();
    }

    [Fact]
    public void endpoint_iterator_is_reliable_over_changes()
    {
        var eps = new EndPointCollection
        {
            { IPAddress.Loopback, 7999 },
            { IPAddress.Loopback, 8000 },
        };

        using var iter = eps.GetEnumerator();
        iter.MoveNext().Should().BeTrue();
        ((IPEndPoint)iter.Current).Port.Should().Be(7999);
        eps[1] = new IPEndPoint(IPAddress.Loopback, 8001); // boom
        iter.MoveNext().Should().BeTrue();
        ((IPEndPoint)iter.Current).Port.Should().Be(8001);
        iter.MoveNext().Should().BeFalse();
    }

    [Theory]
    [InlineData("myDNS:myPort,password=myPassword,connectRetry=3,connectTimeout=15000,syncTimeout=15000,defaultDatabase=0,abortConnect=false,ssl=true,sslProtocols=Tls12", SslProtocols.Tls12)]
    [InlineData("myDNS:myPort,password=myPassword,abortConnect=false,ssl=true,sslProtocols=Tls12", SslProtocols.Tls12)]
    [InlineData("myDNS:myPort,password=myPassword,abortConnect=false,ssl=true,sslProtocols=Ssl3", Ssl3Value)]
    [InlineData("myDNS:myPort,password=myPassword,abortConnect=false,ssl=true,sslProtocols=Tls12 ", SslProtocols.Tls12)]
    public void parse_tls_without_trailing_comma(string configString, SslProtocols expected)
    {
        var config = Parse(configString);
        config.SslProtocols.Should().Be(expected);
    }

    [Theory]
    [InlineData("foo,sslProtocols=NotAThing", "Keyword 'sslProtocols' requires an SslProtocol value (multiple values separated by '|'); the value 'NotAThing' is not recognised.", "sslProtocols")]
    [InlineData("foo,SyncTimeout=ten", "Keyword 'SyncTimeout' requires an integer value; the value 'ten' is not recognised.", "SyncTimeout")]
    [InlineData("foo,syncTimeout=-42", "Keyword 'syncTimeout' has a minimum value of '1'; the value '-42' is not permitted.", "syncTimeout")]
    [InlineData("foo,AllowAdmin=maybe", "Keyword 'AllowAdmin' requires a boolean value; the value 'maybe' is not recognised.", "AllowAdmin")]
    [InlineData("foo,Version=current", "Keyword 'Version' requires a version value; the value 'current' is not recognised.", "Version")]
    [InlineData("foo,proxy=epoxy", "Keyword 'proxy' requires a proxy value; the value 'epoxy' is not recognised.", "proxy")]
    public void config_string_errors_give_meaningful_messages(string configString, string expected, string paramName)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Parse(configString));
        ex.Message.Should().StartWith(expected); // param name gets concatenated sometimes
        ex.ParamName.Should().Be(paramName); // param name gets concatenated sometimes
    }

    [Fact]
    public void config_string_invalid_option_error_give_meaningful_messages()
    {
        var ex = Assert.Throws<ArgumentException>(() => Parse("foo,flibble=value"));
        ex.Message.Should().StartWith("Keyword 'flibble' is not supported."); // param name gets concatenated sometimes
        ex.ParamName.Should().Be("flibble");
    }

    [Fact]
    public void null_apply()
    {
        var options = Parse("127.0.0.1,name=FooApply");
        options.ClientName.Should().Be("FooApply");

        // Doesn't go boom
        var result = options.Apply(null!);
        options.ClientName.Should().Be("FooApply");
        options.Should().Be(result);
    }

    [Fact]
    public void apply()
    {
        var options = Parse("127.0.0.1,name=FooApply");
        options.ClientName.Should().Be("FooApply");

        var randomName = Guid.NewGuid().ToString();
        var result = options.Apply(options => options.ClientName = randomName);

        options.ClientName.Should().Be(randomName);
        result.ClientName.Should().Be(randomName);
        options.Should().Be(result);
    }

    [Fact]
    public async Task before_socket_connect()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        var options = Parse(TestConfig.Current.PrimaryServerAndPort);
        int count = 0;
        options.BeforeSocketConnect = (endpoint, connType, socket) =>
        {
            Interlocked.Increment(ref count);
            Log($"Endpoint: {endpoint}, ConnType: {connType}, Socket: {socket}");
            socket.DontFragment = true;
            socket.Ttl = (short)(connType == ConnectionType.Interactive ? 12 : 123);
        };
        await using var conn = ConnectionMultiplexer.Connect(options);
        conn.IsConnected.Should().BeTrue();
        count.Should().Be(options.TryResp3() ? 1 : 2);

        var endpoint = conn.GetServerSnapshot()[0];
        var interactivePhysical = endpoint.GetBridge(ConnectionType.Interactive)?.TryConnect(null);
        var subscriptionPhysical = endpoint.GetBridge(ConnectionType.Subscription)?.TryConnect(null);
        Assert.NotNull(interactivePhysical);
        Assert.NotNull(subscriptionPhysical);
        var interactiveSocket = interactivePhysical.VolatileSocket;
        var subscriptionSocket = subscriptionPhysical.VolatileSocket;
        Assert.NotNull(interactiveSocket);
        Assert.NotNull(subscriptionSocket);
        interactiveSocket.Ttl.Should().Be(12);
        if (!ReferenceEquals(interactiveSocket, subscriptionSocket))
        {
            subscriptionSocket.Ttl.Should().Be(123);
        }
        interactiveSocket.DontFragment.Should().BeTrue();
        subscriptionSocket.DontFragment.Should().BeTrue();
    }

    /// <summary>
    /// Reads a property that is obsolete-as-error, which the compiler will not let us name directly
    /// (and <c>#pragma warning disable</c> cannot suppress, since it is an error rather than a warning).
    /// </summary>
    private static T GetObsoleteProperty<T>(object target, string name)
    {
        var type = target.GetType();
        var property = type.GetProperty(name);
        if (property is null)
        {
            throw new ArgumentException($"Property '{name}' was not found on '{type.FullName}'; has it been renamed or removed?", nameof(name));
        }

        var value = property.GetValue(target);
        if (value is null)
        {
            // null is a perfectly good value unless the caller asked for a non-nullable value type
            if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) is null)
            {
                throw new ArgumentException($"Property '{type.FullName}.{name}' is null; expected '{typeof(T).Name}'.", nameof(name));
            }

            return default!;
        }

        if (value is not T typed)
        {
            throw new ArgumentException($"Property '{type.FullName}.{name}' is of type '{property.PropertyType.Name}' with value '{value}'; expected '{typeof(T).Name}'.", nameof(name));
        }

        return typed;
    }

    [Fact]
    public async Task mutable_options()
    {
        //gated: this test connects with ConnectionMultiplexer.Connect/ConnectAsync directly rather
        //than through TestBase.Create, so it does not inherit that method's container-tier gate.
        Skip.IfNoContainers();

        var options = Parse(TestConfig.Current.PrimaryServerAndPort + ",name=Details");
        options.LoggerFactory = NullLoggerFactory.Instance;
        var originalConfigChannel = options.ConfigurationChannel = "originalConfig";
        var originalUser = options.User = "originalUser";
        var originalPassword = options.Password = "originalPassword";
        options.ClientName.Should().Be("Details");
        Log(options.ToString());
        await using var conn = await ConnectionMultiplexer.ConnectAsync(options, log: Writer);
        Assert.NotNull(conn.AuthException); //kept as the xUnit form: it carries [NotNull], so the compiler's null-state flows to the dereference below
        Log($"auth failure: {conn.AuthException.Message}");

        // Same instance
        conn.RawConfig.Should().BeSameAs(options);
        // Copies
        conn.EndPoints.Should().NotBeSameAs(options.EndPoints);

        // Same until forked - it's not cloned
        conn.CommandMap.Should().BeSameAs(options.CommandMap);
        options.CommandMap = CommandMap.Envoyproxy;
        conn.CommandMap.Should().NotBeSameAs(options.CommandMap);

        // note: the ConnectionMultiplexer-level versions of these are [Obsolete(..., error: true)], so they
        // can only be reached via reflection - see GetObsoleteProperty
        // Defaults true
        options.IncludeDetailInExceptions.Should().BeTrue();
        (GetObsoleteProperty<bool>(conn, nameof(options.IncludeDetailInExceptions))).Should().BeTrue();
        options.IncludeDetailInExceptions = false;
        options.IncludeDetailInExceptions.Should().BeFalse();
        (GetObsoleteProperty<bool>(conn, nameof(options.IncludeDetailInExceptions))).Should().BeFalse();

        // Defaults false
        options.IncludePerformanceCountersInExceptions.Should().BeFalse();
        (GetObsoleteProperty<bool>(conn, nameof(options.IncludePerformanceCountersInExceptions))).Should().BeFalse();
        options.IncludePerformanceCountersInExceptions = true;
        options.IncludePerformanceCountersInExceptions.Should().BeTrue();
        (GetObsoleteProperty<bool>(conn, nameof(options.IncludePerformanceCountersInExceptions))).Should().BeTrue();

        var newName = Guid.NewGuid().ToString();
        options.ClientName = newName;
        conn.ClientName.Should().Be(newName);

        // TODO: This forks due to memoization of the byte[] for efficiency
        // If we could cheaply detect change it'd be good to let this change
        const string newConfigChannel = "newConfig";
        options.ConfigurationChannel = newConfigChannel;
        options.ConfigurationChannel.Should().Be(newConfigChannel);
        Assert.NotNull(conn.ConfigurationChangedChannel);
        originalConfigChannel.Should().Be(Encoding.UTF8.GetString(conn.ConfigurationChangedChannel));

        conn.RawConfig.User.Should().Be(originalUser);
        conn.RawConfig.Password.Should().Be(originalPassword);
        var newPass = options.Password = "newPassword";
        conn.RawConfig.Password.Should().Be(newPass);
        conn.RawConfig.LoggerFactory.Should().Be(options.LoggerFactory);
        Log("complete");
    }

    [Theory]
    [InlineData("http://somewhere:22", "http:somewhere:22")]
    [InlineData("http:somewhere:22", "http:somewhere:22")]
    public void http_tunnel_can_roundtrip(string input, string expected)
    {
        var config = Parse($"127.0.0.1:6380,tunnel={input}");
        var ip = Assert.IsType<IPEndPoint>(Assert.Single(config.EndPoints));
        ip.Port.Should().Be(6380);
        ip.Address.ToString().Should().Be("127.0.0.1");

        Assert.NotNull(config.Tunnel); //kept as the xUnit form: it carries [NotNull], so the compiler's null-state flows to the dereference below
        config.Tunnel.ToString().Should().Be(expected);

        var cs = config.ToString();
        RemoveTestDefaults(cs).Should().Be($"127.0.0.1:6380,tunnel={expected}");
    }

    private sealed class CustomTunnel : Tunnel { }

    [Fact]
    public void custom_tunnel_can_roundtrip_minus_tunnel()
    {
        // we don't expect to be able to parse custom tunnels, but we should still be able to round-trip
        // the rest of the config, which means ignoring them *in both directions* (unless first party)
        var options = Parse("127.0.0.1,Ssl=true");
        options.Tunnel = new CustomTunnel();
        var cs = options.ToString();
        RemoveTestDefaults(cs).Should().Be("127.0.0.1,ssl=True");
        options = Parse(cs);
        options.Tunnel.Should().BeNull();
    }

    [Theory]
    [InlineData("server:6379", true)]
    [InlineData("server:6379,setlib=True", true)]
    [InlineData("server:6379,setlib=False", false)]
    public void default_config_options_for_set_lib(string configurationString, bool setlib)
    {
        var options = Parse(configurationString);
        options.SetClientLibrary.Should().Be(setlib);
        RemoveTestDefaults(options.ToString()).Should().Be(configurationString);
        options = options.Clone();
        options.SetClientLibrary.Should().Be(setlib);
        RemoveTestDefaults(options.ToString()).Should().Be(configurationString);
    }

    [Theory]
    [InlineData(null, false, "dummy")]
    [InlineData(false, false, "dummy,highIntegrity=False")]
    [InlineData(true, true, "dummy,highIntegrity=True")]
    public void check_high_integrity(bool? assigned, bool expected, string cs)
    {
        var options = Parse("dummy");
        if (assigned.HasValue) options.HighIntegrity = assigned.Value;

        options.HighIntegrity.Should().Be(expected);
        RemoveTestDefaults(options.ToString()).Should().Be(cs);

        var clone = options.Clone();
        clone.HighIntegrity.Should().Be(expected);
        RemoveTestDefaults(clone.ToString()).Should().Be(cs);

        var parsed = Parse(cs);
        parsed.HighIntegrity.Should().Be(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void defaults_provider_protocol_not_serialized(bool clone)
    {
        var options = new ConfigurationOptions();
        var provider = new AzureManagedRedisOptionsProvider();
        options.Defaults = provider;
        if (clone) options = options.Clone();
        options.Protocol.Should().Be(RedisProtocol.Resp3);
        options.Defaults.Should().BeSameAs(provider);
        options.ToString().Should().Be("");
    }
}

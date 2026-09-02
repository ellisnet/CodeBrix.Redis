using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using CodeBrix.Redis.Testing;
using CodeBrix.Redis.Testing.Topologies;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public static class TestConfig
{
    private const string FileName = "RedisTestConfig.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Config Current { get; }

    /// <summary>
    /// Whether real Redis servers are expected to be reachable at the endpoints in
    /// <see cref="Current"/>.
    /// </summary>
    /// <remarks>
    /// NEW in this repository. Upstream assumes a developer started <c>tests/RedisConfigs</c> by
    /// hand and probes each endpoint to find out. Here the servers come from
    /// <see cref="CodeBrix.Redis.Testing.RedisTopologies"/>, which only runs when
    /// <c>CODEBRIX_REDIS_RUN_CONTAINER_TESTS=1</c> is set - so when that gate is off, a
    /// server-backed test skips immediately rather than paying a connect timeout to discover
    /// nothing is there. <see cref="Config.UseExternalServers"/> is the escape hatch that restores
    /// upstream's behaviour for a developer running servers themselves.
    /// </remarks>
    public static bool ServersAvailable =>
        ContainerTier.IsEnabled || Current.UseExternalServers;

    /// <summary>
    /// The sentence a server-backed test skips with when <see cref="ServersAvailable"/> is false.
    /// </summary>
    public static string NoServersReason => ContainerTier.DisabledReason;

    /// <summary>
    /// The PEM-encoded authority certificate for the run's TLS endpoint, or null when the
    /// container tier is off.
    /// </summary>
    /// <remarks>
    /// NEW in this repository, and the reason no server certificate is vendored: upstream's
    /// checked-in TLS material expired in 2023 and cannot be re-signed, so the harness generates a
    /// fresh authority per run and hands it out here. The <c>Certificates/*.pem</c> files that ARE
    /// vendored are unrelated - they back pure certificate-validation unit tests and are good
    /// until 2048.
    /// </remarks>
    public static string? SslCaCertificatePath => RedisTopologyFixture.TlsCaCertificatePath;

    /// <summary>
    /// A floor, in milliseconds, for connection timeouts, from <c>REDIS_TESTS_MIN_TIMEOUT_MS</c>;
    /// zero (the default) leaves the library's own defaults alone.
    /// </summary>
    /// <remarks>
    /// This exists for CI machines that cannot honour the library's 5s defaults for reasons that have
    /// nothing to do with the code under test. It deliberately applies only where a test has not
    /// asked for a specific timeout, so tests that pick a short one to exercise timeout behaviour
    /// keep working.
    /// </remarks>
    public static int MinTimeoutMilliseconds { get; } =
        int.TryParse(Environment.GetEnvironmentVariable("REDIS_TESTS_MIN_TIMEOUT_MS"), out var ms) && ms > 0 ? ms : 0;

    private static int _db = 17;
    public static int GetDedicatedDB(IConnectionMultiplexer? conn = null)
    {
        int db = Interlocked.Increment(ref _db);
        if (conn != null) Skip.IfMissingDatabase(conn, db);
        return db;
    }

    static TestConfig()
    {
        // The suite opens a lot of connections at once (xunit runs 2x cores' worth of collections in
        // parallel), and the thread pool grows only ~1-2 threads per second past its minimum. On a
        // slow or contended machine that ramp is what turns a perfectly healthy server into
        // "Timeout performing PING (5000ms)": a synchronous caller parks waiting for a completion
        // that cannot get a thread. Raising the floor costs nothing on a fast machine, and it is the
        // same advice we give users in docs/Timeouts.md.
        try
        {
            ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
            var target = Math.Max(64, Environment.ProcessorCount * 8);
            ThreadPool.SetMinThreads(Math.Max(workerThreads, target), Math.Max(completionPortThreads, target));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Unable to raise ThreadPool minimums: " + ex.Message);
        }

        Current = new Config();
        try
        {
            using (var stream = typeof(TestConfig).Assembly.GetManifestResourceStream("CodeBrix.Redis.Tests." + FileName))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        //was previously: Newtonsoft's JsonConvert.DeserializeObject<Config>. The
                        //family uses System.Text.Json; the options below are what it needs to read
                        //the same file, which carries // comments as documentation of the fields a
                        //developer may set.
                        Current = JsonSerializer.Deserialize<Config>(reader.ReadToEnd(), JsonOptions)
                            ?? new Config();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error Deserializing TestConfig.json: " + ex);
        }
    }

    public static bool IsServerRunning(string? host, int port)
    {
        if (host.IsNullOrEmpty())
        {
            return false;
        }

        try
        {
            using var client = new TcpClient(host, port);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    /// <remarks>
    /// The default host and port of every field below is taken from
    /// <c>CodeBrix.Redis.Testing</c>'s topology constants rather than written out as a literal, so
    /// that a change to what the harness publishes cannot silently drift away from what the suite
    /// connects to. The values are upstream's - the harness publishes upstream's ports one for one,
    /// on 127.0.0.1, precisely so this suite's expectations survive the move into containers.
    /// </remarks>
    public class Config
    {
        public bool UseSharedConnection { get; set; } = true;
        public bool RunLongRunning { get; set; }

        /// <summary>
        /// Set this to run the suite against Redis servers the developer started themselves,
        /// instead of the container tier. NEW in this repository; see
        /// <see cref="TestConfig.ServersAvailable"/>.
        /// </summary>
        public bool UseExternalServers { get; set; }

        public string PrimaryServer { get; set; } = RedisHarnessOptions.DefaultHost;
        public int PrimaryPort { get; set; } = BasicTopology.PrimaryPort;
        public string PrimaryServerAndPort => PrimaryServer + ":" + PrimaryPort.ToString();

        public string ReplicaServer { get; set; } = RedisHarnessOptions.DefaultHost;
        public int ReplicaPort { get; set; } = BasicTopology.ReplicaPort;
        public string ReplicaServerAndPort => ReplicaServer + ":" + ReplicaPort.ToString();

        public string SecureServer { get; set; } = RedisHarnessOptions.DefaultHost;
        public int SecurePort { get; set; } = SecureTopology.Port;
        public string SecurePassword { get; set; } = SecureTopology.DefaultPassword;
        public string SecureServerAndPort => SecureServer + ":" + SecurePort.ToString();

        // Separate servers for failover tests, so they don't wreak havoc on all others
        public string FailoverPrimaryServer { get; set; } = RedisHarnessOptions.DefaultHost;
        public int FailoverPrimaryPort { get; set; } = FailoverTopology.PrimaryPort;
        public string FailoverPrimaryServerAndPort => FailoverPrimaryServer + ":" + FailoverPrimaryPort.ToString();

        public string FailoverReplicaServer { get; set; } = RedisHarnessOptions.DefaultHost;
        public int FailoverReplicaPort { get; set; } = FailoverTopology.ReplicaPort;
        public string FailoverReplicaServerAndPort => FailoverReplicaServer + ":" + FailoverReplicaPort.ToString();

        public string IPv4Server { get; set; } = RedisHarnessOptions.DefaultHost;
        public int IPv4Port { get; set; } = BasicTopology.PrimaryPort;
        //NOTE: the harness publishes its ports on 127.0.0.1 only, so the IPv6 tests skip unless a
        //developer sets this to something reachable. Upstream ran servers on the host itself.
        public string IPv6Server { get; set; } = "::1";
        public int IPv6Port { get; set; } = BasicTopology.PrimaryPort;

        public string RemoteServer { get; set; } = RedisHarnessOptions.DefaultHost;
        public int RemotePort { get; set; } = BasicTopology.PrimaryPort;
        public string RemoteServerAndPort => RemoteServer + ":" + RemotePort.ToString();

        public string SentinelServer { get; set; } = RedisHarnessOptions.DefaultHost;
        public int SentinelPortA { get; set; } = SentinelTopology.SentinelPortA;
        public int SentinelPortB { get; set; } = SentinelTopology.SentinelPortB;
        public int SentinelPortC { get; set; } = SentinelTopology.SentinelPortC;
        public string SentinelSeviceName { get; set; } = SentinelTopology.ServiceName;

        public string ClusterServer { get; set; } = RedisHarnessOptions.DefaultHost;
        //qualified: the library declares an internal CodeBrix.Redis.ClusterTopology, which the
        //enclosing-namespace walk finds before the harness type of the same name.
        public int ClusterStartPort { get; set; } = Testing.Topologies.ClusterTopology.StartPort;
        public int ClusterServerCount { get; set; } = Testing.Topologies.ClusterTopology.NodeCount;
        public string ClusterServersAndPorts => string.Join(",", Enumerable.Range(ClusterStartPort, ClusterServerCount).Select(port => ClusterServer + ":" + port));

        public string? SslServer { get; set; } = RedisHarnessOptions.DefaultHost;
        public int SslPort { get; set; } = TlsTopology.Port;
        public string SslServerAndPort => SslServer + ":" + SslPort.ToString();

        public string? RedisLabsSslServer { get; set; }
        public int RedisLabsSslPort { get; set; } = 6379;
        public string? RedisLabsPfxPath { get; set; }

        public string? AzureCacheServer { get; set; }
        public string? AzureCachePassword { get; set; }

        public string? SSDBServer { get; set; }
        public int SSDBPort { get; set; } = 8888;

        public string ProxyServer { get; set; } = RedisHarnessOptions.DefaultHost;
        public int ProxyPort { get; set; } = ProxyTopology.Port;

        public string ProxyServerAndPort => ProxyServer + ":" + ProxyPort.ToString();
        public string[] ActiveActiveEndpoints { get; set; } = [];
    }
}

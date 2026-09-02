using System;
using System.IO;

namespace CodeBrix.Redis.Testing;

/// <summary>
/// What the harness starts, where it starts it, and how long it is willing to wait.
/// </summary>
public sealed class RedisHarnessOptions
{
    /// <summary>
    /// The prefix every container, network and label the harness creates carries:
    /// <c>codebrix-redis-test-</c>. Anything with this prefix is the harness's to reuse and to
    /// remove; anything without it is left alone.
    /// </summary>
    public const string ResourcePrefix = "codebrix-redis-test-";

    /// <summary>The label a harness container carries, so a sweep can find it by more than a name.</summary>
    public const string HarnessLabelName = "codebrix.redis.testharness";

    /// <summary>The label holding a container's topology name.</summary>
    public const string TopologyLabelName = "codebrix.redis.topology";

    /// <summary>
    /// The Redis image the harness starts, unless <c>CODEBRIX_REDIS_TEST_IMAGE</c> overrides it.
    /// </summary>
    /// <remarks>
    /// Upstream's compose file builds its server image FROM
    /// <c>redislabs/client-libs-test:custom-30445126297-debian</c>, a Redis-internal CI image whose
    /// tag names one build of one workflow run. That is not something this repository can depend on,
    /// so the harness runs the official image instead. The 3.1.31 suite gates behaviour on server
    /// features up to <c>v8_10_0</c>, and <c>redis:8-alpine</c> is Redis 8.10 - the first official
    /// tag that satisfies every gate the suite has. The alpine variant is chosen for size; it ships
    /// <c>redis-server</c>, <c>redis-cli</c> and TLS support, which is all the harness asks of it.
    /// It has no <c>bash</c>, so every command the harness runs inside a container is
    /// <c>/bin/sh</c>-compatible.
    /// </remarks>
    public const string DefaultImage = "redis:8-alpine";

    /// <summary>
    /// The Envoy image the proxy topology starts, unless
    /// <c>CODEBRIX_REDIS_TEST_PROXY_IMAGE</c> overrides it. Matches the tag upstream's compose file
    /// builds its <c>envoy</c> service from.
    /// </summary>
    public const string DefaultProxyImage = "envoyproxy/envoy:v1.39-latest";

    /// <summary>The host every published port is reachable on.</summary>
    public const string DefaultHost = "127.0.0.1";

    /// <summary>Gets or sets the Redis image to start.</summary>
    public string Image { get; set; } =
        FromEnvironment(ContainerTier.ImageEnvironmentVariableName, DefaultImage);

    /// <summary>Gets or sets the Envoy image the proxy topology starts.</summary>
    public string ProxyImage { get; set; } =
        FromEnvironment(ContainerTier.ProxyImageEnvironmentVariableName, DefaultProxyImage);

    /// <summary>
    /// Gets or sets the host the published ports are reached on. The ported test suite's
    /// <c>TestConfig</c> expects <c>127.0.0.1</c>, and so does every announce address the harness
    /// configures, so changing this is only useful for a daemon that is not on this machine.
    /// </summary>
    public string Host { get; set; } = DefaultHost;

    /// <summary>
    /// Gets or sets the Docker endpoint, or null to let CodeBrix.Docker resolve it from
    /// <c>DOCKER_HOST</c> or the platform default.
    /// </summary>
    public string DockerEndpoint { get; set; }

    /// <summary>Gets or sets how long any one readiness wait may take.</summary>
    public TimeSpan ReadinessTimeout { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Gets or sets how long to wait for the cluster to report <c>cluster_state:ok</c>, which is
    /// slower than every other wait because the nodes gossip before they agree.
    /// </summary>
    public TimeSpan ClusterFormationTimeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// How long to wait, after the cluster reports <c>cluster_state:ok</c>, for every primary in
    /// <c>CLUSTER SLOTS</c> to list at least one replica. This is a courtesy wait, not a
    /// requirement: exceeding it leaves the topology usable and simply means a caller reading
    /// <c>CLUSTER SLOTS</c> in that window sees bare primaries.
    /// </summary>
    public TimeSpan ClusterReplicaVisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets how long to wait for an image pull.</summary>
    public TimeSpan ImagePullTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets a value indicating whether disposing a topology leaves its container running.
    /// Off by default: a run cleans up after itself. Turning it on makes a repeated local run fast,
    /// because every topology then reuses the container the previous run left behind.
    /// </summary>
    public bool LeaveContainersRunning { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="RedisTopologies.StartAllAsync"/> includes
    /// the Envoy proxy topology. It is the only topology needing a second, large image, and the one
    /// upstream test that uses it skips itself when the proxy is absent.
    /// </summary>
    public bool IncludeProxy { get; set; } = true;

    /// <summary>
    /// Gets or sets the folder the run's TLS material is written to. It defaults to a folder under
    /// the system temporary directory, never anywhere in the repository, and it is deliberately
    /// STABLE across runs on one machine: a TLS container adopted from a previous run is already
    /// serving the certificate in this folder, so the tests have to validate against the authority
    /// in it. The material is regenerated when it is missing or within a year of expiring.
    /// </summary>
    public string CertificateDirectory { get; set; } =
        Path.Combine(Path.GetTempPath(), ResourcePrefix + "certs");

    /// <summary>Gets or sets a sink for progress messages, or null for none.</summary>
    public IProgress<string> Progress { get; set; }

    private static string FromEnvironment(string variableName, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}

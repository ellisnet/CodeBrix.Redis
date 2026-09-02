using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Docker;
using CodeBrix.Redis.Testing.Probe;

namespace CodeBrix.Redis.Testing.Topologies;

/// <summary>
/// A monitored pair - a primary on 7010 and its replica on 7011 - watched by three sentinels on
/// 26379, 26380 and 26381.
/// </summary>
/// <remarks>
/// The service name is <c>myprimary</c> and the quorum is 1, both straight from upstream's
/// <c>Sentinel/sentinel-2637x.conf</c>. Every sentinel monitors <c>127.0.0.1 7010</c>, and because
/// all five servers share the container's network namespace that is the address they hand out -
/// which is exactly the address the test process reaches them at through the published ports.
/// Sentinels rewrite their own configuration file as they discover the topology, which is why the
/// harness copies the mounted configuration into the container's writable data directory first.
/// </remarks>
public sealed class SentinelTopology : RedisTopologyBase
{
    /// <summary>The monitored primary's port.</summary>
    public const int PrimaryPort = 7010;

    /// <summary>The monitored replica's port.</summary>
    public const int ReplicaPort = 7011;

    /// <summary>The first sentinel's port, upstream's <c>TestConfig.SentinelPortA</c>.</summary>
    public const int SentinelPortA = 26379;

    /// <summary>The second sentinel's port, upstream's <c>TestConfig.SentinelPortB</c>.</summary>
    public const int SentinelPortB = 26380;

    /// <summary>The third sentinel's port, upstream's <c>TestConfig.SentinelPortC</c>.</summary>
    public const int SentinelPortC = 26381;

    /// <summary>
    /// The monitored service's name, upstream's <c>TestConfig.SentinelSeviceName</c> - spelled that
    /// way upstream, and its VALUE is what matters here.
    /// </summary>
    public const string ServiceName = "myprimary";

    /// <summary>Initializes a new instance of the <see cref="SentinelTopology"/> class.</summary>
    /// <param name="options">The harness options, or null for the defaults.</param>
    /// <param name="client">A Docker client to borrow, or null to create and own one.</param>
    public SentinelTopology(RedisHarnessOptions options = null, DockerClient client = null)
        : base("sentinel", options, client)
    {
    }

    /// <summary>Gets the monitored primary's endpoint.</summary>
    public RedisEndpoint PrimaryEndpoint => Endpoint(PrimaryPort);

    /// <summary>Gets the monitored replica's endpoint.</summary>
    public RedisEndpoint ReplicaEndpoint => Endpoint(ReplicaPort);

    /// <summary>Gets the three sentinel endpoints, in port order.</summary>
    public IReadOnlyList<RedisEndpoint> SentinelEndpoints =>
        [Endpoint(SentinelPortA), Endpoint(SentinelPortB), Endpoint(SentinelPortC)];

    /// <inheritdoc />
    public override IReadOnlyList<RedisEndpoint> Endpoints =>
        [PrimaryEndpoint, ReplicaEndpoint, .. SentinelEndpoints];

    /// <inheritdoc />
    protected override string ConfigFolderName => "Sentinel";

    /// <inheritdoc />
    protected override IReadOnlyList<int> PublishedPorts() =>
        [PrimaryPort, ReplicaPort, SentinelPortA, SentinelPortB, SentinelPortC];

    /// <inheritdoc />
    protected override string BuildStartupScript()
    {
        //The monitored pair goes first: a sentinel that cannot reach its primary at startup spends
        //its down-after-milliseconds budget deciding the primary is down before it ever sees it.
        var script = new StringBuilder(CopyConfigsFragment());
        script.Append(StartServerFragment("redis-7010.conf"));
        script.Append(StartServerFragment("redis-7011.conf"));
        script.Append("sleep 1\n");
        script.Append(StartServerFragment("sentinel-26379.conf", sentinel: true));
        script.Append(StartServerFragment("sentinel-26380.conf", sentinel: true));
        script.Append(StartServerFragment("sentinel-26381.conf", sentinel: true));
        return script.Append("wait\n").ToString();
    }

    /// <inheritdoc />
    protected override async Task WaitForReadyAsync(CancellationToken cancellationToken)
    {
        await WaitAsync("the monitored primary on " + PrimaryEndpoint + " to answer PING",
            token => RedisProbe.PingAsync(PrimaryEndpoint, token), cancellationToken)
            .ConfigureAwait(false);
        await WaitAsync("the monitored replica on " + ReplicaEndpoint + " to report its link as up",
            token => RedisProbe.IsOnlineReplicaAsync(ReplicaEndpoint, token), cancellationToken)
            .ConfigureAwait(false);

        foreach (var sentinel in SentinelEndpoints)
        {
            await WaitAsync("the sentinel on " + sentinel + " to answer PING",
                token => RedisProbe.PingAsync(sentinel, token), cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var sentinel in SentinelEndpoints)
        {
            await WaitAsync(
                "the sentinel on " + sentinel + " to have found " + ServiceName
                + ", its replica and the other two sentinels",
                token => RedisProbe.IsSentinelReadyAsync(sentinel, ServiceName,
                    expectedReplicas: 1, expectedOtherSentinels: SentinelEndpoints.Count - 1, token),
                cancellationToken).ConfigureAwait(false);
        }
    }
}

//Adapted from RedisSetupTool.DockerManagement in the CodeBrix.Docker samples.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Docker;
using CodeBrix.Redis.Testing.Probe;

namespace CodeBrix.Redis.Testing.Topologies;

/// <summary>
/// Six cluster nodes on 7000-7005 - three primaries and three replicas, covering all 16,384 slots.
/// </summary>
/// <remarks>
/// <para>
/// Every node runs with <c>cluster-announce-ip 127.0.0.1</c> and its own port, so a <c>MOVED</c> or
/// <c>ASK</c> redirect names an address the test process can reach: the ports are published to the
/// host one for one. That is the whole reason the six nodes share a container - inside it,
/// <c>127.0.0.1</c> means the same thing to the nodes as it does to the test on the other side of
/// the published port. The cluster bus ports (17000-17005) stay inside the container, where the
/// gossip they carry belongs.
/// </para>
/// <para>
/// The cluster is FORMED once, by <c>redis-cli --cluster create</c> run inside the container. That
/// is the only place the harness leans on the image's contents rather than on the wire, and it is a
/// deliberate trade: hand-assigning slots and replicas over RESP would be a hundred lines
/// reimplementing what redis-cli already does correctly. Forming is skipped when the nodes already
/// report a healthy cluster, which is what makes adopting a container from a previous run work.
/// </para>
/// </remarks>
public sealed class ClusterTopology : RedisTopologyBase
{
    /// <summary>The first node's port, which upstream's <c>TestConfig.ClusterStartPort</c> defaults to.</summary>
    public const int StartPort = 7000;

    /// <summary>The node count, which upstream's <c>TestConfig.ClusterServerCount</c> defaults to.</summary>
    public const int NodeCount = 6;

    /// <summary>How many of the nodes hold slots; the rest replicate them.</summary>
    public const int PrimaryCount = 3;

    /// <summary>Initializes a new instance of the <see cref="ClusterTopology"/> class.</summary>
    /// <param name="options">The harness options, or null for the defaults.</param>
    /// <param name="client">A Docker client to borrow, or null to create and own one.</param>
    public ClusterTopology(RedisHarnessOptions options = null, DockerClient client = null)
        : base("cluster", options, client)
    {
    }

    /// <summary>Gets the six node endpoints, in port order from 7000.</summary>
    public override IReadOnlyList<RedisEndpoint> Endpoints =>
        [.. NodePorts().Select(Endpoint)];

    /// <summary>
    /// Gets the endpoints in the comma-separated form a cluster connection string takes, matching
    /// upstream's <c>TestConfig.ClusterServersAndPorts</c>.
    /// </summary>
    public string EndpointsAndPorts =>
        string.Join(",", Endpoints.Select(endpoint => endpoint.HostAndPort));

    /// <inheritdoc />
    protected override string ConfigFolderName => "Cluster";

    /// <inheritdoc />
    protected override IReadOnlyList<int> PublishedPorts() => NodePorts();

    /// <inheritdoc />
    protected override string BuildStartupScript()
    {
        var script = new StringBuilder(CopyConfigsFragment());
        foreach (var port in NodePorts())
        {
            script.Append(StartServerFragment(
                "cluster-" + port.ToString(CultureInfo.InvariantCulture) + ".conf"));
        }

        return script.Append("wait\n").ToString();
    }

    /// <inheritdoc />
    protected override async Task WaitForReadyAsync(CancellationToken cancellationToken)
    {
        foreach (var endpoint in Endpoints)
        {
            await WaitAsync("the cluster node on " + endpoint + " to answer PING",
                token => RedisProbe.PingAsync(endpoint, token), cancellationToken)
                .ConfigureAwait(false);
        }

        if (!await IsFormedAsync(cancellationToken).ConfigureAwait(false))
        {
            await FormClusterAsync(cancellationToken).ConfigureAwait(false);
        }

        //A freshly created cluster answers cluster_state:fail for a few seconds while the nodes
        //agree, so this is a poll and not a single check.
        await ReadinessWaiter.WaitAsync("every cluster node to report cluster_state:ok",
            Options.ClusterFormationTimeout,
            async token =>
            {
                foreach (var endpoint in Endpoints)
                {
                    if (!await RedisProbe.IsClusterHealthyAsync(endpoint, token).ConfigureAwait(false))
                    {
                        return false;
                    }
                }

                return true;
            },
            cancellationToken).ConfigureAwait(false);

        //cluster_state:ok says the slots are covered; it does NOT say the replicas are visible.
        //A replica is left out of its primary's CLUSTER SLOTS entry until its initial sync
        //completes, so a caller that reads the topology in that window sees bare primaries. Wait
        //for the replicas to appear, but do NOT make it a condition of the topology being usable:
        //every other cluster behaviour is already correct here, and a slow machine must not turn
        //a working cluster into a failed harness. Best effort, bounded, and silent either way.
        try
        {
            await ReadinessWaiter.WaitAsync("every primary in CLUSTER SLOTS to list a replica",
                Options.ClusterReplicaVisibilityTimeout,
                token => RedisProbe.ClusterSlotsListReplicasAsync(Endpoints[0], token),
                cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            //the replicas exist - CLUSTER NODES and CLUSTER SHARDS show them - they are simply
            //not in CLUSTER SLOTS yet. Tests that need them say so for themselves.
        }
    }

    private static IReadOnlyList<int> NodePorts() =>
        [.. Enumerable.Range(StartPort, NodeCount)];

    private async Task<bool> IsFormedAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await RedisProbe.IsClusterHealthyAsync(Endpoints[0], cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false;
        }
    }

    private async Task FormClusterAsync(CancellationToken cancellationToken)
    {
        Report("creating the cluster");
        var command = new List<string> { "redis-cli", "--cluster", "create" };
        foreach (var port in NodePorts())
        {
            command.Add("127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture));
        }

        command.Add("--cluster-replicas");
        command.Add(((NodeCount / PrimaryCount) - 1).ToString(CultureInfo.InvariantCulture));
        command.Add("--cluster-yes");

        var result = await ExecAsync(command, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new HarnessException(
                "redis-cli --cluster create failed with exit code "
                + result.ExitCode.ToString(CultureInfo.InvariantCulture) + ": "
                + Shorten(result.Stdout + result.Stderr));
        }
    }

    private static string Shorten(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "(no output)";
        }

        var trimmed = text.Trim();
        return trimmed.Length > 600 ? trimmed.Substring(0, 600) + "..." : trimmed;
    }
}

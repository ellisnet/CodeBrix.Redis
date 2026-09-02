using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Docker;
using CodeBrix.Redis.Testing.Probe;

namespace CodeBrix.Redis.Testing.Topologies;

/// <summary>
/// A second primary and replica pair, on 6382 and 6383, that tests may promote, demote and generally
/// disturb.
/// </summary>
/// <remarks>
/// Upstream keeps this pair separate from the basic pair for one reason, recorded in its own
/// <c>TestConfig</c>: failover tests rearrange replication, and doing that to 6379/6380 would wreck
/// every other test running against them.
/// </remarks>
public sealed class FailoverTopology : RedisTopologyBase
{
    /// <summary>The primary's port, which upstream's <c>TestConfig.FailoverPrimaryPort</c> defaults to.</summary>
    public const int PrimaryPort = 6382;

    /// <summary>The replica's port, which upstream's <c>TestConfig.FailoverReplicaPort</c> defaults to.</summary>
    public const int ReplicaPort = 6383;

    /// <summary>Initializes a new instance of the <see cref="FailoverTopology"/> class.</summary>
    /// <param name="options">The harness options, or null for the defaults.</param>
    /// <param name="client">A Docker client to borrow, or null to create and own one.</param>
    public FailoverTopology(RedisHarnessOptions options = null, DockerClient client = null)
        : base("failover", options, client)
    {
    }

    /// <summary>Gets the primary's endpoint.</summary>
    public RedisEndpoint PrimaryEndpoint => Endpoint(PrimaryPort);

    /// <summary>Gets the replica's endpoint.</summary>
    public RedisEndpoint ReplicaEndpoint => Endpoint(ReplicaPort);

    /// <inheritdoc />
    public override IReadOnlyList<RedisEndpoint> Endpoints => [PrimaryEndpoint, ReplicaEndpoint];

    /// <inheritdoc />
    protected override string ConfigFolderName => "Failover";

    /// <inheritdoc />
    protected override IReadOnlyList<int> PublishedPorts() => [PrimaryPort, ReplicaPort];

    /// <inheritdoc />
    protected override string BuildStartupScript() =>
        BuildSimpleStartupScript("primary-6382.conf", "replica-6383.conf");

    /// <inheritdoc />
    protected override async Task WaitForReadyAsync(CancellationToken cancellationToken)
    {
        await WaitAsync("the failover primary on " + PrimaryEndpoint + " to answer PING",
            token => RedisProbe.PingAsync(PrimaryEndpoint, token), cancellationToken)
            .ConfigureAwait(false);
        await WaitAsync("the failover replica on " + ReplicaEndpoint + " to answer PING",
            token => RedisProbe.PingAsync(ReplicaEndpoint, token), cancellationToken)
            .ConfigureAwait(false);
        await WaitAsync("the failover replica on " + ReplicaEndpoint + " to report its link as up",
            token => RedisProbe.IsOnlineReplicaAsync(ReplicaEndpoint, token), cancellationToken)
            .ConfigureAwait(false);
    }
}

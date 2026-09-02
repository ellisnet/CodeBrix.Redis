using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Docker;
using CodeBrix.Redis.Testing.Probe;

namespace CodeBrix.Redis.Testing.Topologies;

/// <summary>
/// The pair the bulk of the suite runs against: a primary on 6379 and a replica of it on 6380.
/// </summary>
/// <remarks>
/// Configured from upstream's <c>Basic/primary-6379.conf</c> and <c>Basic/replica-6380.conf</c>,
/// which is why the primary carries 2,000 databases, a 6 GB memory ceiling,
/// <c>notify-keyspace-events AKE</c> and <c>enable-debug-command yes</c> - the suite uses all four.
/// </remarks>
public sealed class BasicTopology : RedisTopologyBase
{
    /// <summary>The primary's port, which upstream's <c>TestConfig.PrimaryPort</c> defaults to.</summary>
    public const int PrimaryPort = 6379;

    /// <summary>The replica's port, which upstream's <c>TestConfig.ReplicaPort</c> defaults to.</summary>
    public const int ReplicaPort = 6380;

    /// <summary>Initializes a new instance of the <see cref="BasicTopology"/> class.</summary>
    /// <param name="options">The harness options, or null for the defaults.</param>
    /// <param name="client">A Docker client to borrow, or null to create and own one.</param>
    public BasicTopology(RedisHarnessOptions options = null, DockerClient client = null)
        : base("basic", options, client)
    {
    }

    /// <summary>Gets the primary's endpoint.</summary>
    public RedisEndpoint PrimaryEndpoint => Endpoint(PrimaryPort);

    /// <summary>Gets the replica's endpoint.</summary>
    public RedisEndpoint ReplicaEndpoint => Endpoint(ReplicaPort);

    /// <inheritdoc />
    public override IReadOnlyList<RedisEndpoint> Endpoints => [PrimaryEndpoint, ReplicaEndpoint];

    /// <inheritdoc />
    protected override string ConfigFolderName => "Basic";

    /// <inheritdoc />
    protected override IReadOnlyList<int> PublishedPorts() => [PrimaryPort, ReplicaPort];

    /// <inheritdoc />
    protected override string BuildStartupScript() =>
        BuildSimpleStartupScript("primary-6379.conf", "replica-6380.conf");

    /// <inheritdoc />
    protected override async Task WaitForReadyAsync(CancellationToken cancellationToken)
    {
        await WaitAsync("the primary on " + PrimaryEndpoint + " to answer PING",
            token => RedisProbe.PingAsync(PrimaryEndpoint, token), cancellationToken)
            .ConfigureAwait(false);
        await WaitAsync("the replica on " + ReplicaEndpoint + " to answer PING",
            token => RedisProbe.PingAsync(ReplicaEndpoint, token), cancellationToken)
            .ConfigureAwait(false);
        await WaitAsync("the replica on " + ReplicaEndpoint + " to report its link as up",
            token => RedisProbe.IsOnlineReplicaAsync(ReplicaEndpoint, token), cancellationToken)
            .ConfigureAwait(false);
        await WaitAsync("the primary on " + PrimaryEndpoint + " to see its replica",
            token => RedisProbe.HasReplicasAsync(PrimaryEndpoint, 1, token), cancellationToken)
            .ConfigureAwait(false);
    }
}

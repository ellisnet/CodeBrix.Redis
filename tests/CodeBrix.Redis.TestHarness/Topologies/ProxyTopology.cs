using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Docker;
using CodeBrix.Redis.Testing.Probe;

namespace CodeBrix.Redis.Testing.Topologies;

/// <summary>
/// Envoy's Redis proxy on 7015, sitting in front of the cluster topology's six nodes, with Envoy's
/// admin interface on 8001.
/// </summary>
/// <remarks>
/// <para>
/// This is the one topology that depends on another: it is worth nothing without
/// <see cref="ClusterTopology"/> behind it. Envoy reaches the cluster by container name over the
/// harness's user-defined bridge network - which is the only reason that network exists - so
/// <c>Configs/Proxy/envoy.yaml</c> is upstream's file with its <c>redis</c> compose-service address
/// replaced by the harness's cluster container name.
/// </para>
/// <para>
/// It is also the least load-bearing topology in the harness. Upstream has exactly one test behind
/// <c>TestConfig.ProxyPort</c>, and that test skips itself when nothing answers. It is here because
/// upstream's compose file defines the service, and it is the first thing to turn off - through
/// <see cref="RedisHarnessOptions.IncludeProxy"/> - on a machine where the extra image is not worth
/// the download.
/// </para>
/// </remarks>
public sealed class ProxyTopology : RedisTopologyBase
{
    /// <summary>The proxied Redis port, which upstream's <c>TestConfig.ProxyPort</c> defaults to.</summary>
    public const int Port = 7015;

    /// <summary>Envoy's own admin port.</summary>
    public const int AdminPort = 8001;

    /// <summary>Initializes a new instance of the <see cref="ProxyTopology"/> class.</summary>
    /// <param name="options">The harness options, or null for the defaults.</param>
    /// <param name="client">A Docker client to borrow, or null to create and own one.</param>
    public ProxyTopology(RedisHarnessOptions options = null, DockerClient client = null)
        : base("proxy", options, client)
    {
    }

    /// <summary>Gets the endpoint a client connects to in place of the cluster.</summary>
    public RedisEndpoint ServerEndpoint => Endpoint(Port);

    /// <summary>Gets Envoy's admin endpoint.</summary>
    public RedisEndpoint AdminEndpoint => Endpoint(AdminPort);

    /// <inheritdoc />
    public override IReadOnlyList<RedisEndpoint> Endpoints => [ServerEndpoint];

    /// <inheritdoc />
    protected override string Image => Options.ProxyImage;

    /// <inheritdoc />
    protected override IReadOnlyList<int> PublishedPorts() => [Port, AdminPort];

    /// <inheritdoc />
    protected override string BuildStartupScript() =>
        "exec envoy -c /etc/envoy/envoy.yaml --log-level warning\n";

    /// <inheritdoc />
    protected override void AddMounts(ContainerSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var configuration = Path.Combine(AppContext.BaseDirectory, "Configs", "Proxy", "envoy.yaml");
        if (!File.Exists(configuration))
        {
            throw new HarnessException(
                "The Envoy configuration " + configuration + " is missing. The harness resolves it"
                + " through AppContext.BaseDirectory, so it has to be copied to the output folder -"
                + " check the <None Update=\"Configs/Proxy/envoy.yaml\" CopyToOutputDirectory> item"
                + " in CodeBrix.Redis.TestHarness.csproj.");
        }

        spec.Mounts.Add(MountSpec.Bind(configuration, "/etc/envoy/envoy.yaml", readOnly: true));
    }

    /// <inheritdoc />
    protected override async Task WaitForReadyAsync(CancellationToken cancellationToken) =>
        await WaitAsync("the Envoy proxy on " + ServerEndpoint + " to answer PING",
            token => RedisProbe.PingAsync(ServerEndpoint, token), cancellationToken)
            .ConfigureAwait(false);
}

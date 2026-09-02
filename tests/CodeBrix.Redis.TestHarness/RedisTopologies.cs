using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Docker;
using CodeBrix.Redis.Testing.Topologies;

namespace CodeBrix.Redis.Testing;

/// <summary>
/// Every topology the ported test suite needs, started together and torn down together.
/// </summary>
/// <remarks>
/// <para>
/// This is what a test fixture holds. Create it once for an assembly, call
/// <see cref="StartAllAsync"/>, hand the tests the endpoints off the topology properties, and let
/// <see cref="DisposeAsync"/> clean up. Starting is idempotent: a container the harness already left
/// running - recognised by the <c>codebrix-redis-test-</c> name prefix - is adopted rather than
/// recreated, so a second run against the same daemon costs a readiness check instead of a cold
/// start.
/// </para>
/// <para>
/// Every topology it starts is gated on <see cref="ContainerTier"/>. A test that reaches for one
/// without checking <see cref="ContainerTier.IsEnabled"/> first will get a
/// <see cref="HarnessException"/> saying so rather than a Docker error.
/// </para>
/// </remarks>
public sealed class RedisTopologies : IAsyncDisposable
{
    private readonly DockerClient _client;
    private bool _disposed;

    private RedisTopologies(RedisHarnessOptions options, DockerClient client)
    {
        Options = options;
        _client = client;
        Basic = new BasicTopology(options, client);
        Secure = new SecureTopology(options, client);
        Tls = new TlsTopology(options, client);
        Failover = new FailoverTopology(options, client);
        Cluster = new ClusterTopology(options, client);
        Sentinel = new SentinelTopology(options, client);
        Proxy = new ProxyTopology(options, client);
    }

    /// <summary>Gets the options every topology was created with.</summary>
    public RedisHarnessOptions Options { get; }

    /// <summary>Gets the primary on 6379 and its replica on 6380.</summary>
    public BasicTopology Basic { get; }

    /// <summary>Gets the password-protected server on 6381.</summary>
    public SecureTopology Secure { get; }

    /// <summary>Gets the TLS-only server on 6384, and the run's certificate authority.</summary>
    public TlsTopology Tls { get; }

    /// <summary>Gets the disposable primary and replica pair on 6382 and 6383.</summary>
    public FailoverTopology Failover { get; }

    /// <summary>Gets the six-node cluster on 7000-7005.</summary>
    public ClusterTopology Cluster { get; }

    /// <summary>Gets the sentinel-monitored pair on 7010 and 7011, watched from 26379-26381.</summary>
    public SentinelTopology Sentinel { get; }

    /// <summary>Gets the Envoy proxy on 7015, which fronts <see cref="Cluster"/>.</summary>
    public ProxyTopology Proxy { get; }

    /// <summary>
    /// Gets every topology in start order. The proxy is last because it needs the cluster behind it.
    /// </summary>
    public IReadOnlyList<RedisTopologyBase> All =>
        [Basic, Secure, Tls, Failover, Cluster, Sentinel, Proxy];

    /// <summary>Creates the set of topologies, without starting anything.</summary>
    /// <param name="options">The harness options, or null for the defaults.</param>
    /// <returns>The topologies; the caller disposes them.</returns>
    /// <exception cref="HarnessException">
    /// The container tier is off. Check <see cref="ContainerTier.IsEnabled"/> and skip with
    /// <see cref="ContainerTier.DisabledReason"/> before calling this.
    /// </exception>
    public static RedisTopologies Create(RedisHarnessOptions options = null)
    {
        if (!ContainerTier.IsEnabled)
        {
            throw new HarnessException(ContainerTier.DisabledReason);
        }

        var resolved = options ?? new RedisHarnessOptions();
        var client = DockerClient.Create(
            string.IsNullOrWhiteSpace(resolved.DockerEndpoint)
                ? null
                : new DockerClientOptions { Endpoint = resolved.DockerEndpoint });
        return new RedisTopologies(resolved, client);
    }

    /// <summary>
    /// Starts every topology the suite needs, adopting whatever is already running, and returns once
    /// all of them have passed their readiness checks.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>How long each topology took, keyed by <see cref="RedisTopologyBase.Name"/>.</returns>
    /// <remarks>
    /// The six Redis topologies start in parallel - they share nothing but the daemon - and the
    /// proxy follows, because it has to find the cluster answering before Envoy's health checking
    /// will let traffic through.
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, TimeSpan>> StartAllAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var timings = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);

        var first = new RedisTopologyBase[] { Basic, Secure, Tls, Failover, Cluster, Sentinel };
        var running = first
            .Select(async topology => new
            {
                topology.Name,
                Elapsed = await topology.StartAsync(cancellationToken).ConfigureAwait(false),
            })
            .ToArray();

        var results = await Task.WhenAll(running).ConfigureAwait(false);
        foreach (var result in results)
        {
            timings[result.Name] = result.Elapsed;
        }

        if (Options.IncludeProxy)
        {
            timings[Proxy.Name] = await Proxy.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        return timings;
    }

    /// <summary>Stops and removes every topology this instance started or adopted.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when they are gone.</returns>
    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var topology in All)
        {
            await topology.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        await RemoveNetworkAsync(_client, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes every container the harness has ever created on this daemon, whoever created it, and
    /// then its network. This is the sweep to run when a previous run was killed part-way.
    /// </summary>
    /// <param name="options">The harness options, or null for the defaults.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The names of the containers that were removed.</returns>
    /// <remarks>
    /// It matches on the <c>codebrix-redis-test-</c> name prefix, so it can find containers left by
    /// a version of the harness that labelled them differently, and it never touches a container
    /// without that prefix.
    /// </remarks>
    public static async Task<IReadOnlyList<string>> RemoveAllAsync(
        RedisHarnessOptions options = null, CancellationToken cancellationToken = default)
    {
        var resolved = options ?? new RedisHarnessOptions();
        using var client = DockerClient.Create(
            string.IsNullOrWhiteSpace(resolved.DockerEndpoint)
                ? null
                : new DockerClientOptions { Endpoint = resolved.DockerEndpoint });

        var removed = new List<string>();
        var containers = await client.Containers
            .ListAsync(all: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        foreach (var container in containers)
        {
            foreach (var rawName in container.Names)
            {
                var name = rawName.TrimStart('/');
                if (!name.StartsWith(RedisHarnessOptions.ResourcePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    await client.Containers
                        .RemoveAsync(name, force: true, removeVolumes: true, cancellationToken)
                        .ConfigureAwait(false);
                    removed.Add(name);
                }
                catch (DockerContainerNotFoundException)
                {
                    //Someone else got there first, which is the outcome asked for.
                }

                break;
            }
        }

        await RemoveNetworkAsync(client, cancellationToken).ConfigureAwait(false);
        return removed;
    }

    /// <summary>
    /// Renders the timings <see cref="StartAllAsync"/> returned as one line per topology, for a
    /// progress log.
    /// </summary>
    /// <param name="timings">The timings.</param>
    /// <returns>The lines, in the order the topologies start.</returns>
    public static IReadOnlyList<string> DescribeTimings(
        IReadOnlyDictionary<string, TimeSpan> timings)
    {
        ArgumentNullException.ThrowIfNull(timings);
        return
        [
            .. timings.Select(pair =>
                pair.Key + ": "
                + pair.Value.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s"),
        ];
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var topology in All)
        {
            await topology.DisposeAsync().ConfigureAwait(false);
        }

        if (!Options.LeaveContainersRunning)
        {
            try
            {
                await RemoveNetworkAsync(_client, CancellationToken.None).ConfigureAwait(false);
            }
            catch (DockerException)
            {
                //Teardown is best effort.
            }
        }

        _client.Dispose();
    }

    private static async Task RemoveNetworkAsync(DockerClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.Networks.RemoveAsync(RedisTopologyBase.NetworkName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DockerApiException)
        {
            //Absent, or still carrying a container someone else owns; neither is this method's
            //problem to solve.
        }
    }
}

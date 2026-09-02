using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CodeBrix.Redis.Testing;
using Xunit;

[assembly: AssemblyFixture(typeof(CodeBrix.Redis.Tests.RedisTopologyFixture))]

namespace CodeBrix.Redis.Tests;

/// <summary>
/// Starts every containerized Redis topology the suite needs, once for the whole assembly, and
/// stops them when the run ends.
/// </summary>
/// <remarks>
/// <para>
/// This file is NEW in this repository - upstream has no equivalent, because upstream expects a
/// developer to have started <c>tests/RedisConfigs</c> by hand (or to be running in CI where a
/// compose file did it). Here the container tier is
/// <see cref="CodeBrix.Redis.Testing.RedisTopologies"/>, and something has to own its lifetime for
/// the run; an xUnit v3 assembly fixture is that owner.
/// </para>
/// <para>
/// It is deliberately cheap when the tier is off. <see cref="ContainerTier.IsEnabled"/> is false
/// unless <c>CODEBRIX_REDIS_RUN_CONTAINER_TESTS=1</c> is set, and in that case this fixture starts
/// nothing, touches no Docker daemon and leaves <see cref="Topologies"/> null. Every server-backed
/// test then skips through <see cref="Skip"/>, with <see cref="ContainerTier.DisabledReason"/> as
/// the reason, before it ever opens a socket.
/// </para>
/// <para>
/// <see cref="Instance"/> exists because upstream's <see cref="TestConfig"/> and <see cref="Skip"/>
/// are static and are reached from roughly two hundred test classes that do not - and should not -
/// take a fixture parameter. xUnit constructs an assembly fixture exactly once before any test
/// runs, so a static handle set in that constructor is safe here in a way it would not be for a
/// class or collection fixture.
/// </para>
/// </remarks>
public sealed class RedisTopologyFixture : IAsyncLifetime
{
    private RedisTopologies? _topologies;

    /// <summary>Initializes a new instance of the <see cref="RedisTopologyFixture"/> class.</summary>
    public RedisTopologyFixture() => Instance = this;

    /// <summary>Gets the fixture xUnit created for this assembly, or null before it is built.</summary>
    public static RedisTopologyFixture? Instance { get; private set; }

    /// <summary>
    /// Gets the running topologies, or null when the container tier is off or has not started yet.
    /// </summary>
    public RedisTopologies? Topologies => _topologies;

    /// <summary>Gets a value indicating whether the topologies are up and answering.</summary>
    public static bool IsRunning => Instance?._topologies is not null;

    /// <summary>
    /// Gets the certificate authority the TLS topology generated for this run, or null when the
    /// container tier is off.
    /// </summary>
    /// <remarks>
    /// TLS material is generated at run time by the harness rather than read from a checked-in
    /// file: upstream's server certificate expired in 2023 and its authority key is not in the
    /// repository, so nothing could re-sign it. A test validating the TLS endpoint puts this
    /// certificate in its <c>X509Chain</c>'s <c>ExtraStore</c> and allows an unknown authority.
    /// </remarks>
    public static X509Certificate2? TlsCaCertificate => Instance?._topologies?.Tls.CaCertificate;

    /// <summary>
    /// Gets the path of the PEM-encoded authority certificate for this run, or null when the
    /// container tier is off.
    /// </summary>
    public static string? TlsCaCertificatePath => Instance?._topologies?.Tls.CaCertificatePath;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (!ContainerTier.IsEnabled)
        {
            //Nothing to start, and deliberately no Docker call at all: a contributor without a
            //daemon still gets a green run from the protocol suite and the in-process server.
            return;
        }

        var topologies = RedisTopologies.Create();
        try
        {
            var timings = await topologies.StartAllAsync().ConfigureAwait(false);
            foreach (var line in RedisTopologies.DescribeTimings(timings))
            {
                Console.WriteLine("Redis topology ready - " + line);
            }

            _topologies = topologies;
        }
        catch
        {
            await topologies.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        var topologies = _topologies;
        _topologies = null;
        Instance = null;
        if (topologies is null)
        {
            return;
        }

        try
        {
            await topologies.StopAllAsync().ConfigureAwait(false);
        }
        finally
        {
            await topologies.DisposeAsync().ConfigureAwait(false);
        }
    }
}

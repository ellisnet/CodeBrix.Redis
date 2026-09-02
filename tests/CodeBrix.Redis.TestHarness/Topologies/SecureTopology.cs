using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Docker;
using CodeBrix.Redis.Testing.Probe;

namespace CodeBrix.Redis.Testing.Topologies;

/// <summary>
/// The password-protected server on 6381, from upstream's <c>Basic/secure-6381.conf</c>.
/// </summary>
public sealed class SecureTopology : RedisTopologyBase
{
    /// <summary>The port, which upstream's <c>TestConfig.SecurePort</c> defaults to.</summary>
    public const int Port = 6381;

    /// <summary>
    /// The password the server requires. It is upstream's, from <c>requirepass changeme</c> in the
    /// configuration file and from <c>TestConfig.SecurePassword</c>; the ported suite expects it,
    /// so it is not a value to improve on.
    /// </summary>
    public const string DefaultPassword = "changeme";

    /// <summary>Initializes a new instance of the <see cref="SecureTopology"/> class.</summary>
    /// <param name="options">The harness options, or null for the defaults.</param>
    /// <param name="client">A Docker client to borrow, or null to create and own one.</param>
    public SecureTopology(RedisHarnessOptions options = null, DockerClient client = null)
        : base("secure", options, client)
    {
    }

    /// <summary>Gets the server's endpoint.</summary>
    public RedisEndpoint ServerEndpoint => Endpoint(Port);

    /// <summary>Gets the password the server requires.</summary>
    public string Password => DefaultPassword;

    /// <inheritdoc />
    public override IReadOnlyList<RedisEndpoint> Endpoints => [ServerEndpoint];

    /// <inheritdoc />
    protected override string ConfigFolderName => "Secure";

    /// <inheritdoc />
    protected override IReadOnlyList<int> PublishedPorts() => [Port];

    /// <inheritdoc />
    protected override string BuildStartupScript() =>
        BuildSimpleStartupScript("secure-6381.conf");

    /// <inheritdoc />
    protected override async Task WaitForReadyAsync(CancellationToken cancellationToken)
    {
        await WaitAsync("the secure server on " + ServerEndpoint + " to answer an authenticated PING",
            token => RedisProbe.AuthenticatedPingAsync(ServerEndpoint, Password, token),
            cancellationToken).ConfigureAwait(false);
        await WaitAsync("the secure server on " + ServerEndpoint + " to refuse an unauthenticated call",
            token => RedisProbe.RequiresPasswordAsync(ServerEndpoint, token), cancellationToken)
            .ConfigureAwait(false);
    }
}

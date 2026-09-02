using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Docker;
using CodeBrix.Redis.Testing.Probe;
using CodeBrix.Redis.Testing.Tls;

namespace CodeBrix.Redis.Testing.Topologies;

/// <summary>
/// The TLS-only server on 6384, from upstream's <c>Basic/tls-ciphers-6384.conf</c>: no plain-text
/// port at all, client certificates not required, and the cipher suite pinned to
/// <c>ECDHE-RSA-AES256-GCM-SHA384</c> over TLS 1.2 and <c>TLS_AES_256_GCM_SHA384</c> over TLS 1.3.
/// </summary>
/// <remarks>
/// The certificates are generated at run time rather than vendored; see
/// <see cref="HarnessCertificateAuthority"/> for why, and for why the folder they live in is
/// stable across runs. The pinned TLS 1.2 suite is an <c>ECDHE-RSA</c> one, so the generated key
/// has to be RSA - which it is.
/// </remarks>
public sealed class TlsTopology : RedisTopologyBase
{
    /// <summary>The port, which upstream's <c>TestConfig.SslPort</c> defaults to.</summary>
    public const int Port = 6384;

    /// <summary>
    /// The name the server certificate is issued for, and the name a client presents in SNI.
    /// </summary>
    public const string CertificateHostName = "localhost";

    private HarnessCertificateAuthority _certificates;

    /// <summary>Initializes a new instance of the <see cref="TlsTopology"/> class.</summary>
    /// <param name="options">The harness options, or null for the defaults.</param>
    /// <param name="client">A Docker client to borrow, or null to create and own one.</param>
    public TlsTopology(RedisHarnessOptions options = null, DockerClient client = null)
        : base("tls", options, client)
    {
    }

    /// <summary>Gets the server's endpoint.</summary>
    public RedisEndpoint ServerEndpoint => Endpoint(Port);

    /// <inheritdoc />
    public override IReadOnlyList<RedisEndpoint> Endpoints => [ServerEndpoint];

    /// <summary>
    /// Gets the run's certificate authority, once the topology has started. Its
    /// <see cref="HarnessCertificateAuthority.CaCertificate"/> is what a test puts in an
    /// <see cref="X509Chain"/> custom trust store to validate the server for real.
    /// </summary>
    public HarnessCertificateAuthority Certificates => _certificates;

    /// <summary>
    /// Gets the authority certificate the server's certificate chains to, or null before the
    /// topology has started.
    /// </summary>
    public X509Certificate2 CaCertificate => _certificates?.CaCertificate;

    /// <summary>Gets the path of the authority certificate in PEM form, for a test that wants the file.</summary>
    public string CaCertificatePath => _certificates?.CaCertificatePath;

    /// <inheritdoc />
    protected override string ConfigFolderName => "Tls";

    /// <inheritdoc />
    protected override IReadOnlyList<int> PublishedPorts() => [Port];

    /// <inheritdoc />
    protected override string BuildStartupScript() =>
        BuildSimpleStartupScript("tls-ciphers-6384.conf");

    /// <inheritdoc />
    protected override Task OnBeforeCreateAsync(CancellationToken cancellationToken)
    {
        _certificates ??= HarnessCertificateAuthority.CreateOrLoad(Options.CertificateDirectory);
        Report("using the certificate authority in " + _certificates.DirectoryPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void AddMounts(ContainerSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        spec.Mounts.Add(MountSpec.Bind(_certificates.DirectoryPath, "/certs", readOnly: true));
    }

    /// <inheritdoc />
    protected override async Task WaitForReadyAsync(CancellationToken cancellationToken) =>
        await WaitAsync(
            "the TLS server on " + ServerEndpoint
            + " to complete a handshake against the run's certificate authority and answer PING",
            token => RedisProbe.SecurePingAsync(ServerEndpoint, CertificateHostName,
                _certificates.CaCertificate, token),
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    protected override void OnDispose()
    {
        _certificates?.Dispose();
        _certificates = null;
    }
}

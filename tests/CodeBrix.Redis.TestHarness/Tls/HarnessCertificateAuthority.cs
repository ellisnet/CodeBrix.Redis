using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CodeBrix.Redis.Testing.Tls;

/// <summary>
/// A certificate authority and a matching server certificate, generated fresh for each harness run
/// and written to disk in the PEM form Redis wants.
/// </summary>
/// <remarks>
/// <para>
/// The upstream repository checks certificates into <c>tests/RedisConfigs/Certs</c>. They are not
/// vendored here, for a reason that cannot be worked around: <c>redis.crt</c> EXPIRED on
/// 2023-08-24, and the authority's private key is not in the repository, so nothing can be
/// re-signed. A checked-in certificate would also expire again on some future day and turn a
/// working suite red for a reason that has nothing to do with the code.
/// </para>
/// <para>
/// Generating the material at run time removes both problems, and it lets the TLS test assert
/// something real: the server certificate chains to THIS authority, which the harness hands the
/// tests, rather than being waved through by a callback that returns true.
/// </para>
/// </remarks>
public sealed class HarnessCertificateAuthority : IDisposable
{
    private const string CaFileName = "ca.crt";
    private const string ServerCertificateFileName = "redis.crt";
    private const string ServerKeyFileName = "redis.key";

    private bool _disposed;

    private HarnessCertificateAuthority(string directoryPath, X509Certificate2 caCertificate)
    {
        DirectoryPath = directoryPath;
        CaCertificate = caCertificate;
    }

    /// <summary>Gets the folder holding <c>ca.crt</c>, <c>redis.crt</c> and <c>redis.key</c>.</summary>
    public string DirectoryPath { get; }

    /// <summary>Gets the path of the authority certificate, in PEM form.</summary>
    public string CaCertificatePath => Path.Combine(DirectoryPath, CaFileName);

    /// <summary>Gets the path of the server certificate, in PEM form.</summary>
    public string ServerCertificatePath => Path.Combine(DirectoryPath, ServerCertificateFileName);

    /// <summary>Gets the path of the server private key, in unencrypted PKCS#8 PEM form.</summary>
    public string ServerKeyPath => Path.Combine(DirectoryPath, ServerKeyFileName);

    /// <summary>
    /// Gets the authority certificate, public part only. A test validating the TLS endpoint puts
    /// this in an <see cref="X509Chain"/> custom trust store.
    /// </summary>
    public X509Certificate2 CaCertificate { get; }

    /// <summary>
    /// Loads the material already in the folder when it is present and still has a year to run, and
    /// generates it otherwise.
    /// </summary>
    /// <param name="directoryPath">
    /// The folder to read and write; it is created when it does not exist.
    /// </param>
    /// <param name="lifetime">How long newly generated certificates are valid for.</param>
    /// <returns>The material; the caller disposes it.</returns>
    /// <remarks>
    /// The folder is stable across runs on one machine, which is what lets the harness adopt a TLS
    /// container a previous run left running: the container is serving the certificate that is in
    /// this folder, so the tests have to validate against the authority that is in it too. It lives
    /// under the system temporary directory, never in the repository.
    /// </remarks>
    public static HarnessCertificateAuthority CreateOrLoad(string directoryPath,
        TimeSpan lifetime = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        var caPath = Path.Combine(directoryPath, CaFileName);
        var certificatePath = Path.Combine(directoryPath, ServerCertificateFileName);
        var keyPath = Path.Combine(directoryPath, ServerKeyFileName);

        if (File.Exists(caPath) && File.Exists(certificatePath) && File.Exists(keyPath))
        {
            X509Certificate2 existingCa = null;
            X509Certificate2 existingServer = null;
            try
            {
                existingCa = X509CertificateLoader.LoadCertificateFromFile(caPath);
                existingServer = X509CertificateLoader.LoadCertificateFromFile(certificatePath);
                var floor = DateTime.Now.AddDays(365);
                if (existingCa.NotAfter > floor && existingServer.NotAfter > floor)
                {
                    existingServer.Dispose();
                    return new HarnessCertificateAuthority(directoryPath, existingCa);
                }
            }
            catch (CryptographicException)
            {
                //Unreadable material is material to replace.
            }

            existingCa?.Dispose();
            existingServer?.Dispose();
        }

        return Create(directoryPath, lifetime);
    }

    /// <summary>
    /// Generates a certificate authority and a server certificate for <c>localhost</c>,
    /// <c>127.0.0.1</c> and <c>::1</c>, and writes all three files into the given folder,
    /// overwriting anything already there.
    /// </summary>
    /// <param name="directoryPath">
    /// The folder to write into; it is created when it does not exist.
    /// </param>
    /// <param name="lifetime">
    /// How long the certificates are valid for. Ten years by default, because an expiry is exactly
    /// the failure this class exists to avoid.
    /// </param>
    /// <returns>The generated material; the caller disposes it.</returns>
    public static HarnessCertificateAuthority Create(string directoryPath, TimeSpan lifetime = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        Directory.CreateDirectory(directoryPath);

        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = notBefore + (lifetime == default ? TimeSpan.FromDays(3650) : lifetime);

        using var caKey = RSA.Create(2048);
        var caRequest = new CertificateRequest("CN=CodeBrix.Redis TestHarness Root CA", caKey,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: true,
                pathLengthConstraint: 1, critical: true));

        //OpenSSL - which is what the Redis server links against - refuses to build a chain through
        //an authority that carries no keyUsage extension, so this is not optional.
        caRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign
                | X509KeyUsageFlags.DigitalSignature,
                critical: true));
        caRequest.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(caRequest.PublicKey, critical: false));

        using var caWithKey = caRequest.CreateSelfSigned(notBefore, notAfter);

        using var serverKey = RSA.Create(2048);
        var serverRequest = new CertificateRequest("CN=localhost", serverKey,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        serverRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: false,
                hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        serverRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));
        serverRequest.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1", "Server Authentication") },
                critical: false));
        serverRequest.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(serverRequest.PublicKey, critical: false));

        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeNames.AddIpAddress(IPAddress.IPv6Loopback);
        serverRequest.CertificateExtensions.Add(subjectAlternativeNames.Build());

        var serialNumber = new byte[8];
        RandomNumberGenerator.Fill(serialNumber);
        serialNumber[0] |= 0x01;
        using var serverCertificate = serverRequest.Create(caWithKey, notBefore, notAfter, serialNumber);

        File.WriteAllText(Path.Combine(directoryPath, CaFileName), caWithKey.ExportCertificatePem());
        File.WriteAllText(Path.Combine(directoryPath, ServerCertificateFileName),
            serverCertificate.ExportCertificatePem());
        File.WriteAllText(Path.Combine(directoryPath, ServerKeyFileName),
            serverKey.ExportPkcs8PrivateKeyPem());

        var caPublicOnly = X509CertificateLoader.LoadCertificate(
            caWithKey.Export(X509ContentType.Cert));
        return new HarnessCertificateAuthority(directoryPath, caPublicOnly);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CaCertificate.Dispose();
    }
}

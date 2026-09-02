using System;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Redis.Tests; //was previously: StackExchange.Redis.Tests;

public class CertValidationTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public void check_issuer_validity()
    {
        // The endpoint cert is the same here
        var endpointCert = LoadCert(Path.Combine("Certificates", "device01.foo.com.pem"));

        // Trusting CA explicitly
        var callback = ConfigurationOptions.TrustIssuerCallback(Path.Combine("Certificates", "ca.foo.com.pem"));
        callback(this, endpointCert, null, SslPolicyErrors.None).Should().BeTrue("subtest 1a");
        callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateChainErrors).Should().BeTrue("subtest 1b");
        callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateNameMismatch).Should().BeFalse("subtest 1c");
        callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateNotAvailable).Should().BeFalse("subtest 1d");
        callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch).Should().BeFalse("subtest 1e");
        callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNotAvailable).Should().BeFalse("subtest 1f");

        // Trusting the remote endpoint cert directly
        callback = ConfigurationOptions.TrustIssuerCallback(Path.Combine("Certificates", "device01.foo.com.pem"));
        callback(this, endpointCert, null, SslPolicyErrors.None).Should().BeTrue("subtest 2a");
        if (Runtime.IsMono)
        {
            // Mono doesn't support this cert usage, reports as rejection (happy for someone to work around this, but isn't high priority)
            callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateChainErrors).Should().BeFalse("subtest 2b");
        }
        else
        {
            callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateChainErrors).Should().BeTrue("subtest 2b");
        }

        callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateNameMismatch).Should().BeFalse("subtest 2c");
        callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateNotAvailable).Should().BeFalse("subtest 2d");
        callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch).Should().BeFalse("subtest 2e");
        callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNotAvailable).Should().BeFalse("subtest 2f");

        // Attempting to trust another CA (mismatch)
        callback = ConfigurationOptions.TrustIssuerCallback(Path.Combine("Certificates", "ca2.foo.com.pem"));
        callback(this, endpointCert, null, SslPolicyErrors.None).Should().BeTrue("subtest 3a");
        callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateChainErrors).Should().BeFalse("subtest 3b");
        callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateNameMismatch).Should().BeFalse("subtest 3c");
        callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateNotAvailable).Should().BeFalse("subtest 3d");
        callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch).Should().BeFalse("subtest 3e");
        callback(this, endpointCert, null, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNotAvailable).Should().BeFalse("subtest 3f");
    }

    //was previously: new X509Certificate2(File.ReadAllBytes(...)) - obsolete (SYSLIB0057); the
    //behaviour-equivalent replacement for a DER/PEM certificate blob is LoadCertificate.
    private static X509Certificate2 LoadCert(string certificatePath) => X509CertificateLoader.LoadCertificate(File.ReadAllBytes(certificatePath));

    [Fact]
    public void check_issuer_args()
    {
        Assert.ThrowsAny<Exception>(() => ConfigurationOptions.TrustIssuerCallback(""));

        var opt = new ConfigurationOptions();
        Assert.Throws<ArgumentNullException>(() => opt.TrustIssuer((X509Certificate2)null!));
    }
}

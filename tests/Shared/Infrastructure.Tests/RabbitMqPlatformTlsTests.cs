using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using His.Hope.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace His.Hope.Infrastructure.Tests;

public sealed class RabbitMqPlatformTlsTests
{
    [Fact]
    public void ApplyPlatformTls_ValidatesServerCertificateWithPeerProvidedIntermediate()
    {
        var now = DateTimeOffset.UtcNow;
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest(
            "CN=OpenShip RabbitMQ Test Root",
            rootKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 1, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            true));
        using var root = rootRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));

        using var intermediateKey = RSA.Create(2048);
        var intermediateRequest = new CertificateRequest(
            "CN=OpenShip RabbitMQ Test Intermediate",
            intermediateKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        intermediateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            true));
        using var issuedIntermediate = intermediateRequest.Create(
            root,
            now.AddMinutes(-5),
            now.AddHours(1),
            RandomNumberGenerator.GetBytes(16));
        using var intermediate = issuedIntermediate.CopyWithPrivateKey(intermediateKey);

        using var serverKey = RSA.Create(2048);
        var serverRequest = new CertificateRequest(
            "CN=openship-rabbitmq.openship-messaging.svc.cluster.local",
            serverKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        serverRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") },
            true));
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("openship-rabbitmq.openship-messaging.svc.cluster.local");
        serverRequest.CertificateExtensions.Add(subjectAlternativeNames.Build());
        using var issuedServer = serverRequest.Create(
            intermediate,
            now.AddMinutes(-5),
            now.AddHours(1),
            RandomNumberGenerator.GetBytes(16));
        using var serverCertificate = issuedServer.CopyWithPrivateKey(serverKey);

        using var peerChain = new X509Chain();
        peerChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        peerChain.ChainPolicy.CustomTrustStore.Add(root);
        peerChain.ChainPolicy.ExtraStore.Add(intermediate);
        peerChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        Assert.True(peerChain.Build(serverCertificate));
        Assert.Equal(3, peerChain.ChainElements.Count);

        var caPath = Path.Combine(Path.GetTempPath(), $"rabbitmq-ca-{Guid.NewGuid():N}.pem");
        File.WriteAllText(caPath, root.ExportCertificatePem());
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["EventBus:UseSsl"] = "true",
                    ["EventBus:CaCertificatePath"] = caPath
                })
                .Build();
            var factory = new ConnectionFactory().ApplyPlatformTls(configuration);
            var callback = Assert.IsType<RemoteCertificateValidationCallback>(factory.Ssl.CertificateValidationCallback);

            var accepted = callback(
                new object(),
                serverCertificate,
                peerChain,
                SslPolicyErrors.RemoteCertificateChainErrors);
            var rejectedNameMismatch = callback(
                new object(),
                serverCertificate,
                peerChain,
                SslPolicyErrors.RemoteCertificateNameMismatch);

            Assert.True(accepted);
            Assert.False(rejectedNameMismatch);
        }
        finally
        {
            File.Delete(caPath);
        }
    }
}

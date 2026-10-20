using His.Hope.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Reflection;
using StackExchange.Redis;

namespace His.Hope.Infrastructure.Tests;

public sealed class RedisConnectionFactoryTests
{
    [Fact]
    public void CreateOptions_UsesConfiguredServiceKeyPrefixForChannels()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:KeyPrefix"] = "project:service:production:"
            })
            .Build();

        var options = RedisConnectionFactory.CreateOptions("redis://redis:6379", configuration);

        Assert.Equal("project:service:production:", options.ChannelPrefix.ToString());
    }

    [Fact]
    public void CreateOptions_RejectsMissingConfiguredCa()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:TlsCaFile"] = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.crt")
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RedisConnectionFactory.CreateOptions("rediss://redis:6379", configuration));

        Assert.Contains("is missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateOptions_ValidatesServerCertificateUsingFullCaBundle()
    {
        var now = DateTimeOffset.UtcNow;
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest(
            "CN=OpenShip Redis Test Root",
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
            "CN=OpenShip Redis Test Issuing CA",
            intermediateKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
        intermediateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            true));
        using var intermediate = intermediateRequest
            .Create(root, now.AddMinutes(-5), now.AddHours(1), RandomNumberGenerator.GetBytes(16))
            .CopyWithPrivateKey(intermediateKey);

        using var serverKey = RSA.Create(2048);
        var serverRequest = new CertificateRequest(
            "CN=redis.test",
            serverKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        serverRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        var serverEku = new OidCollection { new("1.3.6.1.5.5.7.3.1") };
        serverRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(serverEku, true));
        using var serverCertificate = serverRequest
            .Create(intermediate, now.AddMinutes(-5), now.AddHours(1), RandomNumberGenerator.GetBytes(16))
            .CopyWithPrivateKey(serverKey);

        var caPath = Path.Combine(Path.GetTempPath(), $"redis-ca-{Guid.NewGuid():N}.pem");
        File.WriteAllText(caPath, intermediate.ExportCertificatePem() + Environment.NewLine + root.ExportCertificatePem());
        try
        {
            var importedCertificates = new X509Certificate2Collection();
            importedCertificates.ImportFromPem(File.ReadAllText(caPath));
            Assert.Equal(2, importedCertificates.Count);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Redis:TlsCaFile"] = caPath
                })
                .Build();
            var options = RedisConnectionFactory.CreateOptions("rediss://redis.test:6379", configuration);
            var callbackField = typeof(ConfigurationOptions)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(field => field.FieldType == typeof(RemoteCertificateValidationCallback));
            var callback = Assert.IsType<RemoteCertificateValidationCallback>(callbackField.GetValue(options));

            var accepted = callback(
                null,
                serverCertificate,
                null,
                SslPolicyErrors.RemoteCertificateChainErrors);

            Assert.True(accepted);
        }
        finally
        {
            File.Delete(caPath);
        }
    }

    [Fact]
    public void CreateOptions_RejectsPlaintextWhenCaIsConfigured()
    {
        var caPath = Path.GetTempFileName();
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Redis:TlsCaFile"] = caPath
                })
                .Build();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                RedisConnectionFactory.CreateOptions("redis://redis:6379", configuration));

            Assert.Contains("not using TLS", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(caPath);
        }
    }

    [Fact]
    public void CreateOptions_PreservesSentinelServiceAndAclUsernameFromUri()
    {
        var options = RedisConnectionFactory.CreateOptions(
            "rediss://micro-user:synthetic-password@redis-headless:26379/0?serviceName=openship-master",
            new ConfigurationBuilder().Build());

        Assert.Equal("micro-user", options.User);
        Assert.Equal("openship-master", options.ServiceName);
        Assert.Contains("redis-headless", Assert.Single(options.EndPoints).ToString(), StringComparison.Ordinal);
        Assert.True(options.Ssl);
    }

    [Fact]
    public void CreateOptions_UsesConfiguredMutualTlsClientCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=micro-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:TlsClientCertificatePem"] = certificate.ExportCertificatePem(),
                ["Redis:TlsClientPrivateKeyPem"] = key.ExportPkcs8PrivateKeyPem()
            })
            .Build();

        var options = RedisConnectionFactory.CreateOptions("rediss://redis:6379", configuration);
        var tlsOptions = options.SslClientAuthenticationOptions!("redis");

        Assert.IsType<SslClientAuthenticationOptions>(tlsOptions);
        Assert.Single(tlsOptions.ClientCertificates!);
    }

    [Fact]
    public void CreateOptions_RejectsIncompleteMutualTlsConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:TlsClientCertificatePem"] = "synthetic-certificate"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RedisConnectionFactory.CreateOptions("rediss://redis:6379", configuration));

        Assert.Contains("Both Redis client certificate and private key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateOptions_RejectsMutualTlsConfigurationForPlaintextConnection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:TlsClientCertificatePem"] = "synthetic-certificate",
                ["Redis:TlsClientPrivateKeyPem"] = "synthetic-private-key"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RedisConnectionFactory.CreateOptions("redis://redis:6379", configuration));

        Assert.Contains("not using TLS", exception.Message, StringComparison.Ordinal);
    }
}

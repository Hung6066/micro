using His.Hope.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace His.Hope.Infrastructure.Tests;

public sealed class RedisConnectionFactoryTests
{
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

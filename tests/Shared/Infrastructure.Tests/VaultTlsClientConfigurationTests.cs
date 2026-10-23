using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Infrastructure.Tests;

public sealed class VaultTlsClientConfigurationTests
{
    [Fact]
    public void LoadsClientCertificateAndPrivateKey()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=vault-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            var certFile = Path.Combine(directory, "client.pem");
            var keyFile = Path.Combine(directory, "client-key.pem");
            File.WriteAllText(certFile, certificate.ExportCertificatePem());
            File.WriteAllText(keyFile, key.ExportPkcs8PrivateKeyPem());
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Vault:TlsClientCertificateFile"] = certFile,
                ["Vault:TlsClientKeyFile"] = keyFile
            }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            His.Hope.Resilience.HisHopeResilienceExtensions.AddHisHopeResilience(services, configuration);
            His.Hope.Secrets.VaultServiceCollectionExtensions.AddHisHopeVault(services, configuration);
            using var provider = services.BuildServiceProvider();
            using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("vault");
            Assert.NotNull(client);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("TlsClientCertificateFile")]
    [InlineData("TlsClientKeyFile")]
    public void RejectsIncompleteClientIdentity(string field)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [$"Vault:{field}"] = "missing.pem" }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        His.Hope.Resilience.HisHopeResilienceExtensions.AddHisHopeResilience(services, configuration);
        His.Hope.Secrets.VaultServiceCollectionExtensions.AddHisHopeVault(services, configuration);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient("vault"));
        Assert.Contains("both client certificate and key files", error.Message);
    }
}

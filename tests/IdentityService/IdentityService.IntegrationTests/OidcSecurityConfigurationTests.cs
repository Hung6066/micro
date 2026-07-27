using System.Security.Cryptography;
using His.Hope.IdentityService.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public class OidcSecurityConfigurationTests
{
    [Fact]
    public void Resolve_ProductionWithoutPersistentSigningKey_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenIddict:AllowInsecureHttp"] = "false"
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            OidcSecurityConfiguration.Resolve(configuration, new TestHostEnvironment("Production")));

        Assert.Contains("production signing requires a persistent RSA key", ex.Message);
    }

    [Fact]
    public void Resolve_ProductionWithoutPersistentEncryptionKey_Throws()
    {
        var signingKeyPath = WriteRsaPrivateKey();

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenIddict:AllowInsecureHttp"] = "false",
                    ["OpenIddict:Signing:PrivateKeyPath"] = signingKeyPath
                })
                .Build();

            var ex = Assert.Throws<InvalidOperationException>(() =>
                OidcSecurityConfiguration.Resolve(configuration, new TestHostEnvironment("Production")));

            Assert.Contains("production encryption requires a persistent RSA key", ex.Message);
        }
        finally
        {
            File.Delete(signingKeyPath);
        }
    }

    [Fact]
    public void Resolve_DevelopmentWithoutPersistentSigningKey_UsesEphemeralDevelopmentKeys()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = OidcSecurityConfiguration.Resolve(
            configuration,
            new TestHostEnvironment("Development"));

        Assert.True(options.UseEphemeralDevelopmentKeys);
        Assert.True(options.AllowInsecureHttp);
        Assert.Null(options.SigningKey);
        Assert.Null(options.EncryptionKey);
    }

    [Fact]
    public void Resolve_ProductionWithPrivateKey_LoadsPersistentSigningKey()
    {
        var keyPath = Path.Combine(Path.GetTempPath(), $"oidc-signing-{Guid.NewGuid():N}.pem");
        var encryptionKeyPath = Path.Combine(Path.GetTempPath(), $"oidc-encryption-{Guid.NewGuid():N}.pem");

        try
        {
            using var rsa = RSA.Create(2048);
            File.WriteAllText(keyPath, rsa.ExportRSAPrivateKeyPem());
            using var encryptionRsa = RSA.Create(2048);
            File.WriteAllText(encryptionKeyPath, encryptionRsa.ExportRSAPrivateKeyPem());

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenIddict:AllowInsecureHttp"] = "false",
                    ["OpenIddict:Signing:PrivateKeyPath"] = keyPath,
                    ["OpenIddict:Signing:KeyId"] = "test-key",
                    ["OpenIddict:Encryption:PrivateKeyPath"] = encryptionKeyPath,
                    ["OpenIddict:Encryption:KeyId"] = "test-encryption-key"
                })
                .Build();

            var options = OidcSecurityConfiguration.Resolve(
                configuration,
                new TestHostEnvironment("Production"));

            var signingKey = Assert.IsType<RsaSecurityKey>(options.SigningKey);
            Assert.False(options.UseEphemeralDevelopmentKeys);
            Assert.False(options.AllowInsecureHttp);
            Assert.Equal("test-key", signingKey.KeyId);
            var encryptionKey = Assert.IsType<RsaSecurityKey>(options.EncryptionKey);
            Assert.Equal("test-encryption-key", encryptionKey.KeyId);
        }
        finally
        {
            File.Delete(keyPath);
            File.Delete(encryptionKeyPath);
        }
    }

    private static string WriteRsaPrivateKey()
    {
        var keyPath = Path.Combine(Path.GetTempPath(), $"oidc-signing-{Guid.NewGuid():N}.pem");
        using var rsa = RSA.Create(2048);
        File.WriteAllText(keyPath, rsa.ExportRSAPrivateKeyPem());
        return keyPath;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "IdentityService.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

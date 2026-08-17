using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class JwtTokenGeneratorTests
{
    [Fact]
    public void Constructor_RejectsPlaceholderSigningKey()
    {
        using var rsa = RSA.Create(2048);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RsaPrivateKey"] = "${JWT_RSA_PRIVATE_KEY}",
                ["Jwt:RsaEncryptionPrivateKey"] = rsa.ExportPkcs8PrivateKeyPem()
            })
            .Build();

        var act = () => new JwtTokenGenerator(configuration);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Constructor_RejectsWeakSigningKey()
    {
        using var rsa = RSA.Create(2048);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RsaPrivateKey"] = "too-short",
                ["Jwt:RsaEncryptionPrivateKey"] = rsa.ExportPkcs8PrivateKeyPem()
            })
            .Build();

        var act = () => new JwtTokenGenerator(configuration);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void GenerateAccessToken_EmitsIssuerAndAudienceClaims()
    {
        using var rsa = RSA.Create(2048);
        using var signingRsa = RSA.Create(2048);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "http://identity.test",
                ["Jwt:Audience"] = "His.Hope",
                ["Jwt:RsaPrivateKey"] = signingRsa.ExportPkcs8PrivateKeyPem(),
                ["Jwt:RsaEncryptionPrivateKey"] = rsa.ExportPkcs8PrivateKeyPem(),
                ["Jwt:RsaEncryptionKeyId"] = "test-encryption-v1"
            })
            .Build();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "test-user",
            Email = "test-user@hospital.test",
            FirstName = "Test",
            LastName = "User"
        };

        var generator = new JwtTokenGenerator(configuration);
        var (token, _) = generator.GenerateAccessToken(user, Array.Empty<string>());
        Assert.Equal(5, token.Split('.').Length);
        Assert.DoesNotContain(user.Email!, token, StringComparison.Ordinal);
        Assert.DoesNotContain(user.FullName, token, StringComparison.Ordinal);
        var header = token.Split('.')[0].Replace('-', '+').Replace('_', '/');
        header = header.PadRight(header.Length + (4 - header.Length % 4) % 4, '=');
        using var headerJson = JsonDocument.Parse(Convert.FromBase64String(header));
        Assert.Equal(SecurityAlgorithms.RsaOAEP, headerJson.RootElement.GetProperty("alg").GetString());
        Assert.Equal(SecurityAlgorithms.Aes256CbcHmacSha512, headerJson.RootElement.GetProperty("enc").GetString());
        var principal = new JwtTokenGenerator(configuration).GetPrincipalFromExpiredToken(token);
        Assert.NotNull(principal);
        Assert.Equal("human", principal!.FindFirst("principal_type")?.Value);
    }

    [Fact]
    public void ProductionRotation_RetainsOldEncryptionKeyForDecryption()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"jwe-rotation-{Guid.NewGuid():N}"));
        var signingPath = Path.Combine(directory.FullName, "signing.pem");
        var oldEncryptionPath = Path.Combine(directory.FullName, "encryption-old.pem");
        var activeEncryptionPath = Path.Combine(directory.FullName, "encryption-active.pem");
        using var signing = RSA.Create(2048);
        using var oldEncryption = RSA.Create(2048);
        using var activeEncryption = RSA.Create(2048);
        File.WriteAllText(signingPath, signing.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(oldEncryptionPath, oldEncryption.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(activeEncryptionPath, activeEncryption.ExportPkcs8PrivateKeyPem());

        try
        {
            var baseValues = new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "https://identity.his-hope.vn",
                ["Jwt:Audience"] = "his-hope-api",
                ["Jwt:RsaPrivateKeyPath"] = signingPath,
                ["Jwt:RsaEncryptionKeyId"] = "encryption-active-v2"
            };
            var oldConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(baseValues.Concat(new Dictionary<string, string?>
                {
                    ["Jwt:RsaEncryptionPrivateKeyPath"] = oldEncryptionPath
                }))
                .Build();
            var user = new User { Id = Guid.NewGuid(), UserName = "rotation-user", Email = "rotation-user@hospital.test" };
            var (oldToken, _) = new JwtTokenGenerator(oldConfiguration).GenerateAccessToken(user, []);

            var rotatedConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(baseValues.Concat(new Dictionary<string, string?>
                {
                    ["Jwt:RsaEncryptionPrivateKeyPath"] = activeEncryptionPath,
                    ["Jwt:RsaEncryptionPreviousPrivateKey"] = oldEncryption.ExportPkcs8PrivateKeyPem()
                }))
                .Build();
            var rotated = new JwtTokenGenerator(rotatedConfiguration);
            var (newToken, _) = rotated.GenerateAccessToken(user, []);

            Assert.Equal(5, oldToken.Split('.').Length);
            Assert.Equal(5, newToken.Split('.').Length);
            Assert.NotNull(rotated.GetPrincipalFromExpiredToken(oldToken));
            Assert.NotNull(rotated.GetPrincipalFromExpiredToken(newToken));
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }
}

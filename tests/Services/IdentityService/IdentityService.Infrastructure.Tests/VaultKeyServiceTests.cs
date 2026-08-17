using System.Security.Cryptography;
using FluentAssertions;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class VaultKeyServiceTests
{
    [Fact]
    public async Task Development_key_signing_jwks_and_rotation_are_usable()
    {
        using var service = CreateService();
        var data = "identity-test-payload"u8.ToArray();

        var signingKey = await service.GetSigningKeyAsync();
        var signature = Convert.FromBase64String(await service.SignAsync(data));
        using var rsa = ((RsaSecurityKey)signingKey).Rsa!;
        rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Should().BeTrue();
        (await service.GetJwksAsync()).Should().ContainSingle();
        (await service.IsHealthyAsync()).Should().BeTrue();

        await service.RotateKeyAsync();
        (await service.GetJwksAsync()).Should().HaveCount(2);
        (await service.GetSigningKeyAsync()).KeyId.Should().NotBe("jwt-signing");
    }

    [Fact]
    public void Production_requires_a_persistent_signing_key()
    {
        var act = () => new VaultKeyService(
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            NullLogger<VaultKeyService>.Instance,
            new TestHostEnvironment(Environments.Production),
            Mock.Of<IVaultTokenProvider>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*persistent RSA key*");
    }

    [Fact]
    public async Task Health_check_maps_provider_state_to_health_result()
    {
        var provider = new Mock<IVaultKeyProvider>();
        provider.Setup(value => value.IsHealthyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var check = new VaultHealthCheck(provider.Object, NullLogger<VaultHealthCheck>.Instance);

        (await check.CheckHealthAsync(new HealthCheckContext())).Status.Should().Be(HealthStatus.Healthy);

        provider.Setup(value => value.IsHealthyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        (await check.CheckHealthAsync(new HealthCheckContext())).Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Vault_health_returns_false_when_configured_ca_file_is_missing()
    {
        using var service = new VaultKeyService(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Vault:EnableTransit"] = "true",
                ["Vault:Address"] = "https://vault.invalid",
                ["Vault:TlsCaFile"] = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pem")
            }).Build(),
            NullLogger<VaultKeyService>.Instance,
            new TestHostEnvironment(Environments.Development),
            Mock.Of<IVaultTokenProvider>());

        (await service.IsHealthyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Vault_rotation_surfaces_provider_failure_without_silently_succeeding()
    {
        using var service = new VaultKeyService(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Vault:EnableTransit"] = "true",
                ["Vault:Address"] = "https://127.0.0.1:1",
                ["Vault:Transit:KeyName"] = "identity-test"
            }).Build(),
            NullLogger<VaultKeyService>.Instance,
            new TestHostEnvironment(Environments.Development),
            Mock.Of<IVaultTokenProvider>(provider => provider.GetTokenAsync(It.IsAny<CancellationToken>())
                == Task.FromResult("test-token")));

        await Assert.ThrowsAnyAsync<Exception>(() => service.RotateKeyAsync());
        (await service.GetJwksAsync()).Should().HaveCount(2);
    }

    [Fact]
    public void Cleanup_and_dispose_are_idempotent_for_current_key()
    {
        var service = CreateService();
        service.CleanupExpiredKeys();
        service.Dispose();
        service.Dispose();
    }

    private static VaultKeyService CreateService() => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Vault:EnableTransit"] = "false",
            ["OpenIddict:Signing:KeyId"] = "jwt-signing"
        }).Build(),
        NullLogger<VaultKeyService>.Instance,
        new TestHostEnvironment(Environments.Development),
        Mock.Of<IVaultTokenProvider>());

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "IdentityService.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.PhysicalFileProvider(AppContext.BaseDirectory);
    }
}

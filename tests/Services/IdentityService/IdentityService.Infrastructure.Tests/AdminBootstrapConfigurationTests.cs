using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public class AdminBootstrapConfigurationTests
{
    [Fact]
    public void ResolveAdminBootstrapConfiguration_WhenProductionAdminMissingAndSecretMissing_ShouldFailClearly()
    {
        var configuration = BuildConfiguration();

        var act = () => IdentityDbInitializer.ResolveAdminBootstrapConfiguration(
            configuration,
            "Production",
            adminUserExists: false);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Identity:BootstrapAdmin:Password*Production*admin user does not exist*");
    }

    [Fact]
    public void ResolveAdminBootstrapConfiguration_WhenDevelopmentAdminMissingAndSecretMissing_ShouldSkipUserSeed()
    {
        var configuration = BuildConfiguration();

        var result = IdentityDbInitializer.ResolveAdminBootstrapConfiguration(
            configuration,
            "Development",
            adminUserExists: false);

        result.SkipUserSeed.Should().BeTrue();
        result.Password.Should().BeNull();
    }

    [Fact]
    public void ResolveAdminBootstrapConfiguration_WhenSecretConfigured_ShouldUseConfiguredSecret()
    {
        const string configuredSecret = "Configured.Admin.Secret.2026!";
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Identity:BootstrapAdmin:Password"] = configuredSecret
        });

        var result = IdentityDbInitializer.ResolveAdminBootstrapConfiguration(
            configuration,
            "Production",
            adminUserExists: false);

        result.SkipUserSeed.Should().BeFalse();
        result.Password.Should().Be(configuredSecret);
        result.Password.Should().NotBe(string.Concat("Admin", "@", "123"));
    }

    [Fact]
    public void ResolveAdminBootstrapConfiguration_WhenEnvAliasConfigured_ShouldUseConfiguredSecret()
    {
        const string configuredSecret = "Env.Admin.Secret.2026!";
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["IDENTITY_BOOTSTRAP_ADMIN_PASSWORD"] = configuredSecret
        });

        var result = IdentityDbInitializer.ResolveAdminBootstrapConfiguration(
            configuration,
            "Production",
            adminUserExists: false);

        result.SkipUserSeed.Should().BeFalse();
        result.Password.Should().Be(configuredSecret);
    }

    private static IConfiguration BuildConfiguration(IDictionary<string, string?>? values = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();
    }
}

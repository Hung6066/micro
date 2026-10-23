using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class OidcProductionConfigurationContractTests
{
    [Fact]
    public void ProductionConfiguration_RequiresPersistentEncryptionAndHttpsClientUris()
    {
        var configuration = LoadProductionConfiguration();

        configuration["OpenIddict:Issuer"].Should().StartWith("https://");
        configuration.GetValue<bool>("OpenIddict:AllowInsecureHttp").Should().BeFalse();
        configuration["OpenIddict:Encryption:PrivateKeyPath"].Should().NotBeNullOrWhiteSpace();
        configuration["OpenIddict:Encryption:KeyId"].Should().NotBeNullOrWhiteSpace();
        configuration.GetSection("Dpop:RequiredClientIds").Get<string[]>()
            .Should().Contain("his-hope-mobile");
        configuration["Passkeys:RpId"].Should().Be("his-hope.vn");
        var passkeyOrigins = configuration.GetSection("Passkeys:Origins").Get<string[]>();
        passkeyOrigins
            .Should().Contain(new[] { "https://his-hope.vn", "https://dashboard.his-hope.vn", "https://admin.his-hope.vn" });
        passkeyOrigins.Should().OnlyContain(value => IsProductionHttpsUri(value));
        configuration.GetSection("Authentication:RedirectWhitelist").Get<string[]>()
            .Should().OnlyContain(value => IsProductionHttpsUri(value));

        var clients = IdentityDbInitializer.ResolveOidcClientUris(configuration, "Production");

        clients.Should().NotBeEmpty();
        clients.Values.SelectMany(client => client.RedirectUris)
            .Should().OnlyContain(uri => uri.Scheme == Uri.UriSchemeHttps);
        clients.Values.SelectMany(client => client.PostLogoutRedirectUris)
            .Should().OnlyContain(uri => uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool IsProductionHttpsUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        !uri.IsLoopback;

    private static IConfiguration LoadProductionConfiguration() =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("IdentityService.appsettings.json", optional: false)
            .AddJsonFile("IdentityService.appsettings.Production.json", optional: false)
            .Build();
}

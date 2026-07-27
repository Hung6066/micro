using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public class OidcClientRegistrationTests
{
    [Fact]
    public void ResolveOidcClientUris_WhenProductionContainsHttpLocalhostUri_ShouldFail()
    {
        var configuration = BuildConfiguration("http://localhost:4200/auth/callback");

        var act = () => IdentityDbInitializer.ResolveOidcClientUris(configuration, "Production");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Production registrations require HTTPS non-localhost URIs*");
    }

    [Fact]
    public void ResolveOidcClientUris_WhenProductionContainsNonHttpsUri_ShouldFail()
    {
        var configuration = BuildConfiguration("http://identity.example/auth/callback");

        var act = () => IdentityDbInitializer.ResolveOidcClientUris(configuration, "Production");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Production registrations require HTTPS non-localhost URIs*");
    }

    [Fact]
    public void ResolveOidcClientUris_WhenDevelopmentContainsLocalhostUris_ShouldPreserveThem()
    {
        var configuration = BuildConfiguration("http://localhost:4200/auth/callback");

        var clients = IdentityDbInitializer.ResolveOidcClientUris(configuration, "Development");

        clients["his-hope-spa"].RedirectUris.Should().ContainSingle()
            .Which.Should().Be(new Uri("http://localhost:4200/auth/callback"));
    }

    private static IConfiguration BuildConfiguration(string redirectUri)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:OidcClients:his-hope-spa:RedirectUris:0"] = redirectUri,
                ["Authentication:OidcClients:his-hope-spa:PostLogoutRedirectUris:0"] = "https://app.example/auth/login"
            })
            .Build();
    }
}

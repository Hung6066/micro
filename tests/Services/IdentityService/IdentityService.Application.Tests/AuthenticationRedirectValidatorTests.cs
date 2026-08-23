using His.Hope.IdentityService.Application.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class AuthenticationRedirectValidatorTests
{
    [Fact]
    public void ResolveSafeReturnUrl_allows_relative_paths()
    {
        var configuration = BuildConfig(["https://app.example/callback"]);
        Assert.Equal("/dashboard", AuthenticationRedirectValidator.ResolveSafeReturnUrl("/dashboard", configuration));
    }

    [Fact]
    public void ResolveSafeReturnUrl_rejects_unknown_absolute_hosts()
    {
        var configuration = BuildConfig(["https://app.example/callback"]);
        Assert.Equal("/", AuthenticationRedirectValidator.ResolveSafeReturnUrl("https://evil.example/steal", configuration));
    }

    [Fact]
    public void ResolveSafeReturnUrl_allows_whitelisted_absolute_urls()
    {
        var configuration = BuildConfig(["https://app.example/callback"]);
        Assert.Equal(
            "https://app.example/callback/oauth",
            AuthenticationRedirectValidator.ResolveSafeReturnUrl("https://app.example/callback/oauth", configuration));
    }

    private static IConfiguration BuildConfig(string[] whitelist) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:RedirectWhitelist:0"] = whitelist[0]
            })
            .Build();
}

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

    [Fact]
    public void ResolveSafeReturnUrl_resolves_spa_auth_paths_with_spa_origin_hint()
    {
        var configuration = BuildConfig(["http://localhost:4203"]);
        Assert.Equal(
            "http://localhost:4203/auth/login?returnUrl=%2Fdashboard",
            AuthenticationRedirectValidator.ResolveSafeReturnUrl(
                "/auth/login?returnUrl=%2Fdashboard",
                configuration,
                spaOriginHint: "http://localhost:4203"));
    }

    [Fact]
    public void ResolveSafeReturnUrl_rejects_spa_auth_paths_without_origin_hint()
    {
        var configuration = BuildConfig(["http://localhost:4203"]);
        Assert.Equal(
            "/",
            AuthenticationRedirectValidator.ResolveSafeReturnUrl(
                "/auth/login?returnUrl=%2Fdashboard",
                configuration));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveSafeReturnUrl_defaults_empty_values_to_root(string? returnUrl)
    {
        var configuration = BuildConfig([]);

        Assert.Equal("/", AuthenticationRedirectValidator.ResolveSafeReturnUrl(returnUrl, configuration));
    }

    [Theory]
    [InlineData("//evil.example/path")]
    [InlineData("/safe\\evil")]
    [InlineData("/safe:evil")]
    [InlineData("not an absolute url")]
    public void ResolveSafeReturnUrl_rejects_ambiguous_relative_values(string returnUrl)
    {
        var configuration = BuildConfig([]);

        Assert.Equal("/", AuthenticationRedirectValidator.ResolveSafeReturnUrl(returnUrl, configuration));
    }

    [Fact]
    public void ResolveSafeReturnUrl_uses_a_whitelisted_referer_when_hint_is_untrusted()
    {
        var configuration = BuildConfig(["https://app.example"]);

        Assert.Equal(
            "https://app.example/auth/login?returnUrl=%2Fdashboard",
            AuthenticationRedirectValidator.ResolveSafeReturnUrl(
                "/auth/login?returnUrl=%2Fdashboard",
                configuration,
                referer: "https://app.example/signed-in",
                spaOriginHint: "https://evil.example"));
    }

    [Fact]
    public void TryBuildAccountLoginRedirect_reconstructs_account_login_for_legacy_identity_auth_login()
    {
        var configuration = BuildConfig(["http://localhost:4203"]);
        var redirect = AuthenticationRedirectValidator.TryBuildAccountLoginRedirect(
            "/dashboard",
            configuration,
            referer: "http://localhost:4203/auth/login",
            spaOriginHint: "http://localhost:4203");

        Assert.Contains("/Account/Login?", redirect, StringComparison.Ordinal);
        Assert.Contains("spaOrigin=http%3A%2F%2Flocalhost%3A4203", redirect, StringComparison.Ordinal);
        Assert.Contains(
            "returnUrl=http%3A%2F%2Flocalhost%3A4203%2Fauth%2Flogin%3FreturnUrl%3D%252Fdashboard",
            redirect,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TryBuildAccountLoginRedirect_falls_back_without_spa_origin_when_no_origin_is_available()
    {
        var redirect = AuthenticationRedirectValidator.TryBuildAccountLoginRedirect(
            "dashboard",
            BuildConfig([]));

        Assert.Equal("/Account/Login", redirect);
    }

    private static IConfiguration BuildConfig(string[] whitelist)
    {
        var values = new Dictionary<string, string?>();
        for (var i = 0; i < whitelist.Length; i++)
            values[$"Authentication:RedirectWhitelist:{i}"] = whitelist[i];

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}

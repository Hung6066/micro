using FluentAssertions;
using His.Hope.IdentityService.Api.Composition;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

/// <summary>
/// The consent form POST redirects through /connect/authorize to the client's
/// callback; browsers apply CSP form-action to every hop, so the consent page
/// must allow exactly the registered callback origin and nothing broader.
/// </summary>
public sealed class ConsentCspFormActionTests
{
    [Theory]
    [InlineData("https://headscale.nacoms.com.vn/oidc/callback", "https://headscale.nacoms.com.vn")]
    [InlineData("https://dashboard.nacoms.com.vn:8443/api/auth/cb?x=1", "https://dashboard.nacoms.com.vn:8443")]
    [InlineData("http://localhost:8250/oidc/callback", "http://localhost:8250")]
    [InlineData("http://127.0.0.1:4200/cb", "http://127.0.0.1:4200")]
    // Login/MFA pages: a BFF SPA return URL (already whitelisted) maps to the SPA origin.
    [InlineData("https://identity.nacoms.com.vn/auth/login?returnUrl=%2Fclients", "https://identity.nacoms.com.vn")]
    public void Registered_https_or_loopback_callbacks_map_to_their_origin(string redirectUri, string expected)
    {
        IdentityServiceEndpointExtensions.GetCspFormActionOrigin(redirectUri).Should().Be(expected);
    }

    [Theory]
    [InlineData("http://headscale.nacoms.com.vn/oidc/callback")]
    [InlineData("com.hishope.app:/oauth2redirect")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("/relative/callback")]
    [InlineData("")]
    public void Non_https_remote_or_unsafe_callbacks_are_not_added(string redirectUri)
    {
        IdentityServiceEndpointExtensions.GetCspFormActionOrigin(redirectUri).Should().BeNull();
    }
}

using His.Hope.IdentityService.Api.Endpoints;
using OpenIddict.Abstractions;
using System.Security.Claims;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class IdentityOidcClaimDestinationTests
{
    [Theory]
    [InlineData("amr")]
    [InlineData("auth_time")]
    [InlineData("email_verified")]
    // OpenShip console roles: Headscale/Grafana/Vault/Headplane read them from the ID token.
    [InlineData("groups")]
    public void Authentication_assurance_claims_are_available_in_the_identity_token(string claimType)
    {
        var destinations = IdentityOidcAuthorizeEndpoints.ResolveClaimDestinations(claimType);

        Assert.Contains(OpenIddictConstants.Destinations.AccessToken, destinations);
        Assert.Contains(OpenIddictConstants.Destinations.IdentityToken, destinations);
    }

    [Fact]
    public void Email_scope_emits_one_email_verified_and_leaves_email_to_the_issuance_handler()
    {
        // The sign-in handler adds the scalar email claim at issuance; adding it here
        // too produced a JSON array that Headscale cannot decode.
        var identity = new ClaimsIdentity([new Claim(OpenIddictConstants.Claims.EmailVerified, "false")]);

        IdentityOidcAuthorizeEndpoints.AddEmailScopeClaims(identity, " pilot@example.test ", emailConfirmed: true);

        Assert.Empty(identity.FindAll(OpenIddictConstants.Claims.Email));
        Assert.Equal("true", Assert.Single(identity.FindAll(OpenIddictConstants.Claims.EmailVerified)).Value);
        Assert.Contains(OpenIddictConstants.Destinations.IdentityToken,
            IdentityOidcAuthorizeEndpoints.ResolveClaimDestinations(OpenIddictConstants.Claims.Email));
    }

    [Fact]
    public void Email_scope_reports_unverified_and_omits_missing_email()
    {
        var unverified = new ClaimsIdentity();
        IdentityOidcAuthorizeEndpoints.AddEmailScopeClaims(unverified, "pilot@example.test", emailConfirmed: false);
        Assert.Equal("false", Assert.Single(unverified.FindAll(OpenIddictConstants.Claims.EmailVerified)).Value);

        var missing = new ClaimsIdentity([new Claim(OpenIddictConstants.Claims.EmailVerified, "true")]);
        IdentityOidcAuthorizeEndpoints.AddEmailScopeClaims(missing, null, emailConfirmed: true);
        Assert.Empty(missing.FindAll(OpenIddictConstants.Claims.EmailVerified));
    }

    [Fact]
    public void Authentication_freshness_requires_a_recent_auth_time_claim()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var fresh = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("auth_time", (now.ToUnixTimeSeconds() - 120).ToString())
        ]));
        var stale = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("auth_time", (now.ToUnixTimeSeconds() - 301).ToString())
        ]));
        var missing = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.True(IdentityOidcAuthorizeEndpoints.HasFreshAuthentication(fresh, 300, now));
        Assert.False(IdentityOidcAuthorizeEndpoints.HasFreshAuthentication(stale, 300, now));
        Assert.False(IdentityOidcAuthorizeEndpoints.HasFreshAuthentication(missing, 300, now));
        Assert.False(IdentityOidcAuthorizeEndpoints.HasFreshAuthentication(fresh, 0, now));
        Assert.False(IdentityOidcAuthorizeEndpoints.RequiresFreshAuthentication(fresh, 300, false, now));
        Assert.True(IdentityOidcAuthorizeEndpoints.RequiresFreshAuthentication(stale, 300, false, now));
        Assert.True(IdentityOidcAuthorizeEndpoints.RequiresFreshAuthentication(fresh, 0, false, now));
        Assert.True(IdentityOidcAuthorizeEndpoints.RequiresFreshAuthentication(stale, null, true, now));
    }
}

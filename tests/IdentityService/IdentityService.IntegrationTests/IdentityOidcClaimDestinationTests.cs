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
    public void Authentication_assurance_claims_are_available_in_the_identity_token(string claimType)
    {
        var destinations = IdentityOidcAuthorizeEndpoints.ResolveClaimDestinations(claimType);

        Assert.Contains(OpenIddictConstants.Destinations.AccessToken, destinations);
        Assert.Contains(OpenIddictConstants.Destinations.IdentityToken, destinations);
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

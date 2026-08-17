using System.Security.Claims;
using His.Hope.IdentityService.Application.Scim;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class ScimAuthorizationTests
{
    [Fact]
    public void ClientCredentialsToken_WithScimScope_IsAccepted()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("scope", "openid scim.read"),
            new Claim("client_id", "scim-provisioner")
        ], "Bearer"));

        Assert.True(ScimAuthorization.HasProvisioningScope(principal));
    }

    [Fact]
    public void CookieOrUserToken_WithoutScimScope_IsRejected()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(ClaimTypes.Role, "Admin")], "Cookies"));

        Assert.False(ScimAuthorization.HasProvisioningScope(principal));
    }

    [Fact]
    public void AuthenticatedToken_WithOnlyOpenIdScope_IsRejected()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("scope", "openid profile"),
            new Claim("client_id", "regular-client")
        ], "Bearer"));

        Assert.False(ScimAuthorization.HasProvisioningScope(principal));
        Assert.False(ScimAuthorization.HasScope(principal, ScimAuthorization.ReadScope));
    }

    [Fact]
    public void WriteScope_GrantsProvisioningButNotReadScope()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim("scope", "scim.write")], "Bearer"));

        Assert.True(ScimAuthorization.HasProvisioningScope(principal));
        Assert.True(ScimAuthorization.HasScope(principal, ScimAuthorization.WriteScope));
        Assert.False(ScimAuthorization.HasScope(principal, ScimAuthorization.ReadScope));
    }
}

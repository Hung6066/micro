using System.Security.Claims;
using FluentAssertions;
using His.Hope.IdentityService.Application.Scim;
using Xunit;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class ScimAuthorizationTests
{
    [Fact]
    public void HasProvisioningScope_AcceptsAuthenticatedClientWithDedicatedScope()
    {
        var principal = CreatePrincipal($"openid {ScimAuthorization.ReadScope}");

        ScimAuthorization.HasProvisioningScope(principal).Should().BeTrue();
    }

    [Fact]
    public void HasProvisioningScope_RejectsAuthenticatedClientWithoutDedicatedScope()
    {
        var principal = CreatePrincipal("openid hishop:admin");

        ScimAuthorization.HasProvisioningScope(principal).Should().BeFalse();
    }

    [Fact]
    public void HasProvisioningScope_RejectsUnauthenticatedPrincipal()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        ScimAuthorization.HasProvisioningScope(principal).Should().BeFalse();
    }

    [Fact]
    public void HasScope_DoesNotTreatReadAsWrite()
    {
        var principal = CreatePrincipal($"openid {ScimAuthorization.ReadScope}");

        ScimAuthorization.HasScope(principal, ScimAuthorization.ReadScope).Should().BeTrue();
        ScimAuthorization.HasScope(principal, ScimAuthorization.WriteScope).Should().BeFalse();
    }

    private static ClaimsPrincipal CreatePrincipal(string scope) =>
        new(new ClaimsIdentity([new Claim("scope", scope)], "Bearer"));
}

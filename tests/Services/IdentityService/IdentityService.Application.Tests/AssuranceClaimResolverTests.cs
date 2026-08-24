using System.Security.Claims;
using His.Hope.IdentityService.Application.Assurance;
using Xunit;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class AssuranceClaimResolverTests
{
    [Fact]
    public void Resolve_assurance_prefers_mtls_over_other_methods()
    {
        var principal = Principal("pwd", "mfa", "mtls");

        Assert.Equal("aal3", AssuranceClaimResolver.ResolveAssuranceLevel(principal));
    }

    [Theory]
    [InlineData("passkey")]
    [InlineData("mfa")]
    [InlineData("totp")]
    [InlineData("webauthn")]
    public void Resolve_assurance_maps_strong_authentication_methods_to_aal2(string method)
    {
        Assert.Equal("aal2", AssuranceClaimResolver.ResolveAssuranceLevel(Principal(method)));
    }

    [Theory]
    [InlineData("pwd")]
    [InlineData("password")]
    public void Resolve_assurance_maps_password_methods_to_aal1(string method)
    {
        Assert.Equal("aal1", AssuranceClaimResolver.ResolveAssuranceLevel(Principal(method)));
    }

    [Fact]
    public void Resolve_assurance_ignores_empty_and_unknown_methods()
    {
        var principal = Principal(" ", "unknown");

        Assert.Equal("standard", AssuranceClaimResolver.ResolveAssuranceLevel(principal));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData(null, false)]
    public void Device_posture_claim_is_strictly_interpreted(string? value, bool expected)
    {
        var claims = value is null ? Array.Empty<Claim>() : [new Claim("device_posture_fresh", value)];

        Assert.Equal(expected, AssuranceClaimResolver.HasFreshDevicePosture(
            new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))));
    }

    private static ClaimsPrincipal Principal(params string[] methods) =>
        new(new ClaimsIdentity(methods.Select(method => new Claim("amr", method)), "test"));
}

using System.Security.Claims;
using His.Hope.IdentityService.Application.Assurance;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class AssuranceClaimResolverTests
{
    [Theory]
    [InlineData("mtls", "aal3")]
    [InlineData("MTLS", "aal3")]
    [InlineData("  ", "standard")]
    [InlineData("passkey", "aal2")]
    [InlineData("PASSKEY", "aal2")]
    [InlineData("mfa", "aal2")]
    [InlineData("totp", "aal2")]
    [InlineData("webauthn", "aal2")]
    [InlineData("pwd", "aal1")]
    [InlineData("password", "aal1")]
    [InlineData("", "standard")]
    public void ResolveAssuranceLevel_maps_amr_to_assurance(string amr, string expected)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("amr", amr)], "test"));

        Assert.Equal(expected, AssuranceClaimResolver.ResolveAssuranceLevel(principal));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void HasFreshDevicePosture_requires_true_or_one(string? value, bool expected)
    {
        var claims = value is null ? [] : new[] { new Claim("device_posture_fresh", value) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        Assert.Equal(expected, AssuranceClaimResolver.HasFreshDevicePosture(principal));
    }

    [Fact]
    public void ResolveAssuranceLevel_prefers_mtls_over_lower_assurance_methods()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("amr", "pwd"), new Claim("amr", "mfa"), new Claim("amr", "mtls")], "test"));

        Assert.Equal("aal3", AssuranceClaimResolver.ResolveAssuranceLevel(principal));
    }

    [Fact]
    public void ResolveAssuranceLevel_ignores_unknown_and_blank_methods()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("amr", ""), new Claim("amr", "unknown")], "test"));

        Assert.Equal("standard", AssuranceClaimResolver.ResolveAssuranceLevel(principal));
    }

    [Fact]
    public void HasFreshDevicePosture_returns_false_when_claim_is_missing()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.False(AssuranceClaimResolver.HasFreshDevicePosture(principal));
    }
}

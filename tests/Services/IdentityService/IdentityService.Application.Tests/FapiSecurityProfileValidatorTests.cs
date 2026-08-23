using His.Hope.IdentityService.Application.Security;
using Xunit;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class FapiSecurityProfileValidatorTests
{
    [Fact]
    public void Validate_AcceptsCompliantConfidentialDpopClient()
    {
        var registration = new FapiSecurityProfileValidator.FapiClientRegistration(
            "partner-a",
            "private_key_jwt",
            true,
            true,
            ["https://partner.example/callback"],
            ["authorization_code", "refresh_token"]);

        var result = FapiSecurityProfileValidator.Validate(registration);
        Assert.True(result.IsCompliant);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Validate_RejectsPublicClientAndPasswordGrant()
    {
        var registration = new FapiSecurityProfileValidator.FapiClientRegistration(
            "legacy",
            "none",
            false,
            false,
            ["https://partner.example/callback"],
            ["password"]);

        var result = FapiSecurityProfileValidator.Validate(registration);
        Assert.False(result.IsCompliant);
        Assert.Contains(result.Violations, violation => violation.Contains("confidential", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Violations, violation => violation.Contains("password", StringComparison.OrdinalIgnoreCase));
    }
}

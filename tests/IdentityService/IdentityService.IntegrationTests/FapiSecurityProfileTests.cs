using His.Hope.IdentityService.Application.Security;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class FapiSecurityProfileTests
{
    [Fact]
    public void FapiProfile_RejectsBrowserSpaRegistration()
    {
        var registration = new FapiSecurityProfileValidator.FapiClientRegistration(
            "his-hope-spa",
            "none",
            false,
            false,
            ["https://localhost/callback"],
            ["authorization_code", "refresh_token"]);

        var result = FapiSecurityProfileValidator.Validate(registration);
        Assert.False(result.IsCompliant);
    }

    [Fact]
    public void FapiProfile_AcceptsRegulatedPartnerRegistration()
    {
        var registration = new FapiSecurityProfileValidator.FapiClientRegistration(
            "regulated-partner",
            "private_key_jwt",
            true,
            true,
            ["https://partner-regulated.example/oauth/callback"],
            ["authorization_code", "refresh_token"]);

        var result = FapiSecurityProfileValidator.Validate(registration);
        Assert.True(result.IsCompliant);
    }
}

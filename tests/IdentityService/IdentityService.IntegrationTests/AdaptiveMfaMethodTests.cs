using FluentAssertions;
using His.Hope.IdentityService.Api.Services;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class AdaptiveMfaMethodTests
{
    [Fact]
    public void Recognized_device_with_passkey_prefers_passkey()
    {
        var result = AdaptiveMfaMethodPolicy.Resolve(
            hasPasskey: true, hasMobileApproval: true, hasTotp: true, unfamiliarDevice: false);

        result.PreferredMethod.Should().Be("passkey");
        result.AvailableMethods.Should().BeEquivalentTo("passkey", "mobileApproval", "totp");
    }

    [Fact]
    public void Unfamiliar_device_prefers_mobile_approval()
    {
        var result = AdaptiveMfaMethodPolicy.Resolve(
            hasPasskey: true, hasMobileApproval: true, hasTotp: true, unfamiliarDevice: true);

        result.PreferredMethod.Should().Be("mobileApproval");
    }

    [Fact]
    public void Totp_is_available_only_when_enrolled()
    {
        var result = AdaptiveMfaMethodPolicy.Resolve(
            hasPasskey: false, hasMobileApproval: false, hasTotp: false, unfamiliarDevice: false);

        result.AvailableMethods.Should().BeEmpty();
    }
}

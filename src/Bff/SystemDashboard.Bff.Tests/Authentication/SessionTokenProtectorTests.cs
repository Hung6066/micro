using His.Hope.Bff.Core.Authentication;
using Microsoft.AspNetCore.DataProtection;

namespace SystemDashboard.Bff.Tests.Authentication;

public sealed class SessionTokenProtectorTests
{
    [Fact]
    public void Protects_tokens_and_round_trips_with_the_same_key_ring()
    {
        var provider = DataProtectionProvider.Create("His.Hope.IdentityService");
        var protector = new SessionTokenProtector(provider);

        var protectedToken = protector.Protect("refresh-token");

        Assert.StartsWith("dp:v1:", protectedToken, StringComparison.Ordinal);
        Assert.NotEqual("refresh-token", protectedToken);
        Assert.Equal("refresh-token", protector.Unprotect(protectedToken));
    }

    [Fact]
    public void Rejects_unprotected_legacy_payloads()
    {
        var provider = DataProtectionProvider.Create("His.Hope.IdentityService");
        var protector = new SessionTokenProtector(provider);

        Assert.Throws<System.Security.Cryptography.CryptographicException>(
            () => protector.Unprotect("plaintext-refresh-token"));
    }
}

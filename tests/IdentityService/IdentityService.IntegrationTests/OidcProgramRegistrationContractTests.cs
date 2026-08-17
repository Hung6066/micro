using FluentAssertions;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class OidcProgramRegistrationContractTests
{
    [Fact]
    public void OpenIddictServerRegistration_ExposesRevocationAndSingleUseReferenceRefreshTokens()
    {
        var registration = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "IdentityServiceRegistrationExtensions.cs"));

        registration.Should().Contain("options.SetRevocationEndpointUris(IdentityApiRoutes.OidcRevoke)");
        IdentityApiRoutes.OidcRevoke.Should().Be("/connect/revoke");
        registration.Should().Contain("options.UseReferenceRefreshTokens()");
        registration.Should().Contain("options.SetRefreshTokenReuseLeeway(TimeSpan.Zero)");
        registration.Should().NotContain("DisableAccessTokenEncryption");
        registration.Should().Contain("options.AddEncryptionKey(encryptionKey)");
        registration.Should().Contain("oidcSecurity.EncryptionKey is not null");
        registration.Should().Contain("DpopTokenBindingHandler.Descriptor");
        registration.Should().Contain("DpopTokenResponseHandler.Descriptor");
    }
}

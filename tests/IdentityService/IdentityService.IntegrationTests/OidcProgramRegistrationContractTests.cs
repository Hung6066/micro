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

        registration.Should().Contain("options.SetRevocationEndpointUris(\"/connect/revoke\")");
        registration.Should().Contain("options.UseReferenceRefreshTokens()");
        registration.Should().Contain("options.SetRefreshTokenReuseLeeway(TimeSpan.Zero)");
    }
}

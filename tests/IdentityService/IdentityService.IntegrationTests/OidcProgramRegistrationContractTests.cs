using FluentAssertions;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class OidcProgramRegistrationContractTests
{
    [Fact]
    public void OpenIddictServerRegistration_ExposesRevocationAndSingleUseReferenceRefreshTokens()
    {
        var program = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "IdentityService.Program.cs"));

        program.Should().Contain("options.SetRevocationEndpointUris(\"/connect/revoke\")");
        program.Should().Contain("options.UseReferenceRefreshTokens()");
        program.Should().Contain("options.SetRefreshTokenReuseLeeway(TimeSpan.Zero)");
    }
}

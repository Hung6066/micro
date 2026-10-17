using System.Security.Claims;
using His.Hope.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace His.Hope.Infrastructure.Tests;

public sealed class OpenFgaCanaryAuthorizerTests
{
    [Fact]
    public async Task AllowsAsync_returns_true_when_mode_is_shadow()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AUTHZ_PDP_MODE"] = "shadow" })
            .Build();
        var openFga = new Mock<IOpenFgaClient>();
        var authorizer = new OpenFgaCanaryAuthorizer(openFga.Object, NullLogger<OpenFgaCanaryAuthorizer>.Instance, configuration);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")], "Bearer"));

        var allowed = await authorizer.AllowsAsync(principal, "identity.admin.read");

        Assert.True(allowed);
        openFga.Verify(x => x.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AllowsAsync_denies_when_canary_external_check_is_false()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AUTHZ_PDP_MODE"] = "canary" })
            .Build();
        var openFga = new Mock<IOpenFgaClient>();
        openFga.Setup(x => x.CheckAsync("user:user-1", "identity_admin_read", "permission:identity.admin.read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var authorizer = new OpenFgaCanaryAuthorizer(openFga.Object, NullLogger<OpenFgaCanaryAuthorizer>.Instance, configuration);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")], "Bearer"));

        var allowed = await authorizer.AllowsAsync(principal, "identity.admin.read");

        Assert.False(allowed);
    }

    [Fact]
    public async Task AllowsAsync_denies_when_canary_pdp_is_unavailable()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AUTHZ_PDP_MODE"] = "canary" })
            .Build();
        var openFga = new Mock<IOpenFgaClient>();
        openFga.Setup(x => x.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((bool?)null);
        var authorizer = new OpenFgaCanaryAuthorizer(openFga.Object, NullLogger<OpenFgaCanaryAuthorizer>.Instance, configuration);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")], "Bearer"));

        var allowed = await authorizer.AllowsAsync(principal, "identity.admin.read");

        Assert.False(allowed);
    }

    [Fact]
    public async Task AllowsAsync_denies_when_canary_subject_is_missing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AUTHZ_PDP_MODE"] = "canary" })
            .Build();
        var openFga = new Mock<IOpenFgaClient>();
        var authorizer = new OpenFgaCanaryAuthorizer(openFga.Object, NullLogger<OpenFgaCanaryAuthorizer>.Instance, configuration);

        var allowed = await authorizer.AllowsAsync(
            new ClaimsPrincipal(new ClaimsIdentity([], "Bearer")), "identity.admin.read");

        Assert.False(allowed);
        openFga.Verify(x => x.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

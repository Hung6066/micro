using FluentAssertions;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Application.OpenIddict;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class OpenIddictValidateAuthorizationRequestTests
{
    [Fact]
    public async Task Disabled_registry_does_not_validate_request()
    {
        var registry = new Mock<IConglomerateTenantRegistry>();
        registry.SetupGet(item => item.IsEnabled).Returns(false);
        var context = Context("client-a");

        await Handler(registry.Object).HandleAsync(context);

        context.Error.Should().BeNull();
        registry.Verify(item => item.IsConglomerateClient(It.IsAny<string?>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Missing_client_id_does_not_validate_request(string? clientId)
    {
        var registry = new Mock<IConglomerateTenantRegistry>();
        registry.SetupGet(item => item.IsEnabled).Returns(true);
        var context = Context(clientId);

        await Handler(registry.Object).HandleAsync(context);

        context.Error.Should().BeNull();
        registry.Verify(item => item.IsConglomerateClient(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Non_conglomerate_client_is_left_unchanged()
    {
        var registry = new Mock<IConglomerateTenantRegistry>();
        registry.SetupGet(item => item.IsEnabled).Returns(true);
        registry.Setup(item => item.IsConglomerateClient("public-client")).Returns(false);
        var context = Context("public-client");

        await Handler(registry.Object).HandleAsync(context);

        context.Error.Should().BeNull();
        registry.Verify(item => item.GetClientTenant(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Bound_conglomerate_client_is_accepted()
    {
        var registry = new Mock<IConglomerateTenantRegistry>();
        registry.SetupGet(item => item.IsEnabled).Returns(true);
        registry.Setup(item => item.IsConglomerateClient("tenant-client")).Returns(true);
        registry.Setup(item => item.GetClientTenant("tenant-client")).Returns("tenant-a");
        var context = Context("tenant-client");

        await Handler(registry.Object).HandleAsync(context);

        context.Error.Should().BeNull();
    }

    [Fact]
    public async Task Conglomerate_client_without_binding_is_rejected_as_invalid_client()
    {
        var registry = new Mock<IConglomerateTenantRegistry>();
        registry.SetupGet(item => item.IsEnabled).Returns(true);
        registry.Setup(item => item.IsConglomerateClient("unbound-client")).Returns(true);
        registry.Setup(item => item.GetClientTenant("unbound-client")).Returns((string?)null);
        var context = Context("unbound-client");

        await Handler(registry.Object).HandleAsync(context);

        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidClient);
        context.ErrorDescription.Should().Contain("not bound to a tenant");
    }

    private static CustomValidateAuthorizationRequest Handler(IConglomerateTenantRegistry registry) =>
        new(registry, NullLogger<CustomValidateAuthorizationRequest>.Instance);

    private static OpenIddictServerEvents.ValidateAuthorizationRequestContext Context(string? clientId)
    {
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest { ClientId = clientId }
        };
        return new OpenIddictServerEvents.ValidateAuthorizationRequestContext(transaction);
    }
}

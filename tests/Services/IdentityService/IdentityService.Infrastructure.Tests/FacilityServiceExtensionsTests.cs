using FluentAssertions;
using His.Hope.IdentityService.Infrastructure.Facility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class FacilityServiceExtensionsTests
{
    [Fact]
    public void AddFacilityBoundary_registers_context_accessor_and_singleton_handler()
    {
        var services = new ServiceCollection();

        services.AddFacilityBoundary();

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(FacilityContext)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IHttpContextAccessor));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IAuthorizationHandler)
            && descriptor.ImplementationType == typeof(FacilityAuthorizationHandler)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddFacilityBoundary_registers_default_and_strict_policies()
    {
        var provider = new ServiceCollection()
            .AddFacilityBoundary()
            .BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationOptions>>().Value;

        options.GetPolicy("Facility").Should().NotBeNull();
        options.GetPolicy("Facility:Strict").Should().NotBeNull();
        options.GetPolicy("Facility")!.Requirements.Should().ContainSingle()
            .Which.Should().BeOfType<FacilityRequirement>()
            .Which.StrictMode.Should().BeFalse();
        options.GetPolicy("Facility:Strict")!.Requirements.Should().ContainSingle()
            .Which.Should().BeOfType<FacilityRequirement>()
            .Which.StrictMode.Should().BeTrue();
    }
}

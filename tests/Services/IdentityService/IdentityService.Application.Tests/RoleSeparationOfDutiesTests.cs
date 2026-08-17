using FluentAssertions;
using His.Hope.IdentityService.Application.Authorization;
using Xunit;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class RoleSeparationOfDutiesTests
{
    [Fact]
    public void Rejects_provider_and_billing_roles_together()
    {
        RoleSeparationOfDuties.TryFindConflict(["Provider", "BillingClerk"], out var conflict)
            .Should().BeTrue();
        conflict.Should().Be("Provider + BillingClerk");
    }

    [Fact]
    public void Allows_unrelated_roles()
    {
        RoleSeparationOfDuties.TryFindConflict(["Provider", "Nurse"], out _)
            .Should().BeFalse();
    }
}

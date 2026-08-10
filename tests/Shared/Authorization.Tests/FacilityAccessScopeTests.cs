using System.Security.Claims;
using FluentAssertions;
using His.Hope.Authorization;
using His.Hope.Authorization.Handlers;
using His.Hope.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace His.Hope.Authorization.Tests;

public sealed class FacilityAccessScopeTests
{
    [Fact]
    public void FromPrincipal_combines_primary_and_multi_facility_claims()
    {
        var principal = Principal(
            ("facility_id", "facility-a"),
            ("facility_ids", "facility-a, facility-b"));

        var scope = FacilityAccessScope.FromPrincipal(principal);

        scope.CanAccess("facility-a").Should().BeTrue();
        scope.CanAccess("facility-b").Should().BeTrue();
        scope.CanAccess("facility-c").Should().BeFalse();
    }

    [Fact]
    public void Cross_facility_requires_the_explicit_permission()
    {
        var principal = Principal(("permissions", "patients.view"));

        FacilityAccessScope.FromPrincipal(principal).IsCrossFacility.Should().BeFalse();
        FacilityAccessScope.FromPrincipal(Principal(("permissions", "facility.cross")))
            .IsCrossFacility.Should().BeTrue();
    }

    [Fact]
    public async Task Permission_handler_denies_role_only_token()
    {
        var user = Principal((ClaimTypes.Role, "Admin"));
        var requirement = new PermissionRequirement("patients.view");
        var context = new AuthorizationHandlerContext([requirement], user, null);
        var handler = new PermissionHandler(NullLogger<PermissionHandler>.Instance);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(claims.Select(claim => new Claim(claim.Type, claim.Value)), "test");
        return new ClaimsPrincipal(identity);
    }
}

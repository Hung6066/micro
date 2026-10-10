using System.Security.Claims;
using FluentAssertions;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class RoleGovernanceEvaluatorTests
{
    [Fact]
    public async Task ValidateRolePermissions_allows_permissions_held_by_actor_claims()
    {
        await using var db = CreateDb();
        var actor = Principal(Guid.NewGuid(), "clinical.read");

        var result = await RoleGovernanceEvaluator.ValidateRolePermissionsAsync(
            db, actor, [" clinical.read "], CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateRolePermissions_rejects_permission_outside_actor_scope()
    {
        await using var db = CreateDb();
        var actor = Principal(Guid.NewGuid(), "clinical.read");

        var result = await RoleGovernanceEvaluator.ValidateRolePermissionsAsync(
            db, actor, ["billing.void"], CancellationToken.None);

        result.Should().Contain("ROLE_GRANT_OUT_OF_SCOPE").And.Contain("billing.void");
    }

    [Fact]
    public async Task ValidateRolePermissions_loads_actor_permissions_from_database_when_claims_are_absent()
    {
        await using var db = CreateDb();
        var actorId = Guid.NewGuid();
        var role = new Role { Id = Guid.NewGuid(), Name = "Auditor", NormalizedName = "AUDITOR" };
        db.Roles.Add(role);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = actorId, RoleId = role.Id });
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = "audit.read" });
        await db.SaveChangesAsync();

        var result = await RoleGovernanceEvaluator.ValidateRolePermissionsAsync(
            db, Principal(actorId, string.Empty), ["audit.read"], CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateRoleAssignment_resolves_role_by_id_and_enforces_facility_scope()
    {
        await using var db = CreateDb();
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var role = new Role { Id = Guid.NewGuid(), Name = "Clinician", NormalizedName = "CLINICIAN" };
        db.Roles.Add(role);
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = "clinical.read" });
        db.UserFacilities.AddRange(
            new UserFacility { UserId = actorId, FacilityId = "facility-a", IsActive = true },
            new UserFacility { UserId = targetId, FacilityId = "facility-b", IsActive = true });
        await db.SaveChangesAsync();

        var result = await RoleGovernanceEvaluator.ValidateRoleAssignmentAsync(
            db, Principal(actorId, "clinical.read"), targetId, [role.Id.ToString()], CancellationToken.None);

        result.Should().StartWith("FACILITY_SCOPE_DENIED");
    }

    [Fact]
    public async Task ValidateRoleAssignment_allows_cross_facility_permission()
    {
        await using var db = CreateDb();
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var role = new Role { Id = Guid.NewGuid(), Name = "Clinician", NormalizedName = "CLINICIAN" };
        db.Roles.Add(role);
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = "clinical.read" });
        db.UserFacilities.AddRange(
            new UserFacility { UserId = actorId, FacilityId = "facility-a", IsActive = true },
            new UserFacility { UserId = targetId, FacilityId = "facility-b", IsActive = true });
        await db.SaveChangesAsync();

        var result = await RoleGovernanceEvaluator.ValidateRoleAssignmentAsync(
            db, Principal(actorId, "clinical.read facility.cross"), targetId, ["Clinician"], CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateRoleAssignment_reports_unknown_role()
    {
        await using var db = CreateDb();

        var result = await RoleGovernanceEvaluator.ValidateRoleAssignmentAsync(
            db, Principal(Guid.NewGuid(), "admin.permissions.write"), Guid.NewGuid(), ["missing-role"], CancellationToken.None);

        result.Should().StartWith("ROLE_NOT_FOUND");
    }

    [Fact]
    public async Task ValidateRoleAssignment_reports_unknown_guid_role()
    {
        await using var db = CreateDb();

        var result = await RoleGovernanceEvaluator.ValidateRoleAssignmentAsync(
            db, Principal(Guid.NewGuid(), "admin.permissions.write"), Guid.NewGuid(), [Guid.NewGuid().ToString()], CancellationToken.None);

        result.Should().StartWith("ROLE_NOT_FOUND");
    }

    private static IdentityDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"role-governance-{Guid.NewGuid():N}")
            .Options;
        return new IdentityDbContext(options);
    }

    private static ClaimsPrincipal Principal(Guid id, string permissions) => new(
        new ClaimsIdentity(
            [new Claim("sub", id.ToString()), new Claim("permissions", permissions)],
            "test"));
}

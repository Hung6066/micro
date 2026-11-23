using System.Security.Claims;
using FluentAssertions;
using His.Hope.Authorization;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.SharedKernel.Authorization;
using His.Hope.SharedKernel.Protocol;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests.Authorization;

public sealed class IamTenantAccessGuardTests
{
    [Fact]
    public void EnsureCrossTenantRead_allows_hq_operator_with_direct_membership()
    {
        var user = UserWithMemberships("nacoms-group", "manufacturing");
        var filter = new IamTenantScopeFilter(
            [Guid.NewGuid()],
            ["manufacturing"],
            IsGroupHqOperator: true,
            AccessDenied: false);
        var policy = new ConfigurableCrossTenantAccessPolicy(
        [
            new CrossTenantAllowedPair("nacoms-group", "manufacturing", "audit", ["admin.audit.read"])
        ]);

        var result = IamTenantAccessGuard.EnsureCrossTenantRead(
            user,
            filter,
            "admin.users.read",
            policy);

        result.Should().BeNull();
    }

    [Fact]
    public void EnsureCrossTenantRead_denies_hq_operator_without_membership_for_non_audit_action()
    {
        var user = UserWithMemberships("nacoms-group");
        var filter = new IamTenantScopeFilter(
            [Guid.NewGuid()],
            ["manufacturing"],
            IsGroupHqOperator: true,
            AccessDenied: false);
        var policy = new ConfigurableCrossTenantAccessPolicy(
        [
            new CrossTenantAllowedPair("nacoms-group", "manufacturing", "audit", ["admin.audit.read"])
        ]);

        var result = IamTenantAccessGuard.EnsureCrossTenantRead(
            user,
            filter,
            "admin.users.read",
            policy);

        result.Should().NotBeNull();
        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>();
    }

    [Fact]
    public void EnsureCrossTenantRead_allows_hq_operator_without_membership_for_audit_action()
    {
        var user = UserWithMemberships("nacoms-group");
        var filter = new IamTenantScopeFilter(
            [Guid.NewGuid()],
            ["manufacturing"],
            IsGroupHqOperator: true,
            AccessDenied: false);
        var policy = new ConfigurableCrossTenantAccessPolicy(
        [
            new CrossTenantAllowedPair("nacoms-group", "manufacturing", "audit", ["admin.audit.read"])
        ]);

        var result = IamTenantAccessGuard.EnsureCrossTenantRead(
            user,
            filter,
            "admin.audit.read",
            policy);

        result.Should().BeNull();
    }

    [Fact]
    public void ForbidIfDenied_returns_forbid_when_access_denied()
    {
        var filter = new IamTenantScopeFilter([], [], false, AccessDenied: true);

        var result = IamTenantAccessGuard.ForbidIfDenied(filter);

        result.Should().NotBeNull();
        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>();
    }

    [Fact]
    public async Task ResolveForReadAsync_narrows_unscoped_hq_read_to_the_hq_tenant_instead_of_forbidding()
    {
        var (db, hqId, manufacturingId) = await SeedTenantsAsync();
        await using var _ = db;
        var user = UserWithMemberships("nacoms-group");
        var policy = new ConfigurableCrossTenantAccessPolicy(
        [
            new CrossTenantAllowedPair("nacoms-group", "manufacturing", "audit", ["admin.audit.read"])
        ]);

        var (filter, error) = await IamTenantAccessGuard.ResolveForReadAsync(
            db, user, scopeId: null, "admin.breakglass.read", policy, EnabledRegistry().Object, CancellationToken.None);

        error.Should().BeNull();
        filter.AllowedTenantKeys.Should().BeEquivalentTo(["nacoms-group"]);
        filter.AllowedScopeIds.Should().Contain(hqId).And.NotContain(manufacturingId);
    }

    [Fact]
    public async Task ResolveForReadAsync_still_forbids_an_explicitly_requested_tenant_without_a_grant()
    {
        var (db, _, manufacturingId) = await SeedTenantsAsync();
        await using var __ = db;
        var user = UserWithMemberships("nacoms-group");
        var policy = new ConfigurableCrossTenantAccessPolicy(
        [
            new CrossTenantAllowedPair("nacoms-group", "manufacturing", "audit", ["admin.audit.read"])
        ]);

        var (_, error) = await IamTenantAccessGuard.ResolveForReadAsync(
            db, user, manufacturingId, "admin.breakglass.read", policy, EnabledRegistry().Object, CancellationToken.None);

        error.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>();
    }

    private static async Task<(IdentityDbContext Db, Guid HqId, Guid ManufacturingId)> SeedTenantsAsync()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new IdentityDbContext(options);
        var hqId = Guid.NewGuid();
        var manufacturingId = Guid.NewGuid();
        db.IamScopes.Add(new IamScope { Id = hqId, Key = "nacoms-group", DisplayName = "Nacoms Group", Kind = "tenant", IsActive = true });
        db.IamScopes.Add(new IamScope { Id = manufacturingId, Key = "manufacturing", DisplayName = "Manufacturing", Kind = "tenant", IsActive = true });
        await db.SaveChangesAsync();
        return (db, hqId, manufacturingId);
    }

    private static Moq.Mock<His.Hope.IdentityService.Application.Conglomerate.IConglomerateTenantRegistry> EnabledRegistry()
    {
        var registry = new Moq.Mock<His.Hope.IdentityService.Application.Conglomerate.IConglomerateTenantRegistry>();
        registry.SetupGet(x => x.IsEnabled).Returns(true);
        registry.SetupGet(x => x.HqCustomerVisibility).Returns("none");
        registry.Setup(x => x.IsCustomerTenant(Moq.It.IsAny<string>())).Returns(false);
        registry.Setup(x => x.GetClientIdsForTenant(Moq.It.IsAny<string>())).Returns([]);
        return registry;
    }

    private static ClaimsPrincipal UserWithMemberships(params string[] memberships)
    {
        var claims = memberships
            .Select(membership => new Claim(IamTenantScopeResolver.TenantMembershipClaimType, membership))
            .ToList();
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}

public sealed class IamTenantScopeResolverTests
{
    [Fact]
    public async Task ResolveAsync_denies_authenticated_human_platform_admin_without_memberships()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new IdentityDbContext(options);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(HisHopeProtocolConstants.Claims.SuperAdmin, "true"),
            new Claim(AuthorizationConstants.Claims.PrincipalType, AuthorizationConstants.PrincipalTypes.Human)
        ], "test"));

        var filter = await IamTenantScopeResolver.ResolveAsync(db, user, null, CancellationToken.None);

        filter.AccessDenied.Should().BeTrue();
        filter.IsRestricted.Should().BeTrue();
        IamTenantScopeResolver.GetMemberships(user).Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public async Task ResolveAsync_denies_incomplete_platform_admin_identity(
        bool authenticated, bool adminRole, bool superAdmin, bool human)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new IdentityDbContext(options);
        var claims = new List<Claim>();
        if (adminRole) claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        if (superAdmin) claims.Add(new Claim(HisHopeProtocolConstants.Claims.SuperAdmin, "true"));
        claims.Add(new Claim(AuthorizationConstants.Claims.PrincipalType,
            human ? AuthorizationConstants.PrincipalTypes.Human : AuthorizationConstants.PrincipalTypes.Workload));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null));

        var filter = await IamTenantScopeResolver.ResolveAsync(db, user, null, CancellationToken.None);

        filter.AccessDenied.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveAsync_denies_non_hq_user_without_memberships()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new IdentityDbContext(options);
        db.IamScopes.Add(new IamScope
        {
            Id = Guid.NewGuid(),
            Key = "manufacturing",
            DisplayName = "Manufacturing",
            Kind = "tenant",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var user = new ClaimsPrincipal(new ClaimsIdentity([], "test"));
        var filter = await IamTenantScopeResolver.ResolveAsync(db, user, null, CancellationToken.None);

        filter.AccessDenied.Should().BeTrue();
        filter.AllowedTenantKeys.Should().BeEmpty();
    }
}

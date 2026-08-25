using FluentAssertions;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests.Authorization;

public sealed class IamTenantQueryExtensionsTests
{
    [Fact]
    public async Task User_membership_queries_are_case_insensitive_and_fail_closed_for_empty_scope()
    {
        await using var db = CreateDb();
        var memberId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        db.Users.AddRange(new User { Id = memberId, UserName = "member" }, new User { Id = otherId, UserName = "other" });
        db.UserClaims.Add(new IdentityUserClaim<Guid>
        {
            UserId = memberId,
            ClaimType = IamTenantScopeResolver.TenantMembershipClaimType,
            ClaimValue = "Tenant-A"
        });
        await db.SaveChangesAsync();

        var matched = await db.Users.WhereTenantMembership(db, new HashSet<string>(["tenant-a"])).Select(x => x.Id).ToListAsync();
        matched.Should().ContainSingle().Which.Should().Be(memberId);

        (await db.Users.WhereTenantMembership(db, []).CountAsync()).Should().Be(2);
        (await db.Users.WhereTenantMembership(db, null).CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task User_has_tenant_access_handles_null_empty_match_and_mismatch()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.UserClaims.Add(new IdentityUserClaim<Guid>
        {
            UserId = userId,
            ClaimType = IamTenantScopeResolver.TenantMembershipClaimType,
            ClaimValue = "Tenant-A"
        });
        await db.SaveChangesAsync();

        (await IamTenantQueryExtensions.UserHasTenantAccessAsync(db, userId, null, CancellationToken.None)).Should().BeTrue();
        (await IamTenantQueryExtensions.UserHasTenantAccessAsync(db, userId, [], CancellationToken.None)).Should().BeFalse();
        (await IamTenantQueryExtensions.UserHasTenantAccessAsync(db, userId, ["TENANT-A"], CancellationToken.None)).Should().BeTrue();
        (await IamTenantQueryExtensions.UserHasTenantAccessAsync(db, userId, ["tenant-b"], CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public void Client_resolution_returns_distinct_clients_for_each_allowed_tenant()
    {
        var registry = new Mock<IConglomerateTenantRegistry>();
        registry.Setup(x => x.GetClientIdsForTenant("tenant-a")).Returns(["client-a", "shared"]);
        registry.Setup(x => x.GetClientIdsForTenant("tenant-b")).Returns(["shared", "client-b"]);

        IamTenantQueryExtensions.ResolveAllowedClientIds(registry.Object,
            new IamTenantScopeFilter([], ["tenant-a", "tenant-b"], false, false))
            .Should().BeEquivalentTo(["client-a", "client-b", "shared"]);
        IamTenantQueryExtensions.ResolveAllowedClientIds(registry.Object,
            new IamTenantScopeFilter([], null, false, false)).Should().BeNull();
    }

    [Fact]
    public async Task Policy_owner_resolution_handles_missing_scopes_and_hq_identity_owner()
    {
        await using var db = CreateDb();
        var scope = Guid.NewGuid();
        db.IamResourcePolicies.AddRange(
            new IamResourcePolicy { ScopeId = scope, ServiceKey = "service-a" },
            new IamResourcePolicy { ScopeId = scope, ServiceKey = "SERVICE-A" },
            new IamResourcePolicy { ScopeId = Guid.NewGuid(), ServiceKey = "other" });
        await db.SaveChangesAsync();

        (await IamTenantQueryExtensions.ResolveTenantPolicyOwnersAsync(
            db, new IamTenantScopeFilter([scope], ["tenant-a"], false, false), CancellationToken.None))
            .Should().ContainSingle().Which.Should().Be("service-a");

        (await IamTenantQueryExtensions.ResolveTenantPolicyOwnersAsync(
            db, new IamTenantScopeFilter([], ["tenant-a"], false, false), CancellationToken.None))
            .Should().BeEmpty();

        (await IamTenantQueryExtensions.ResolveTenantPolicyOwnersAsync(
            db, new IamTenantScopeFilter([scope], [IamTenantScopeResolver.GroupHqTenantKey], true, false), CancellationToken.None))
            .Should().Contain("identity-service");

        (await IamTenantQueryExtensions.ResolveTenantPolicyOwnersAsync(
            db, new IamTenantScopeFilter(null, null, false, false), CancellationToken.None))
            .Should().BeNull();
    }

    [Fact]
    public async Task Audit_actor_filter_uses_tenant_membership_and_preserves_unrestricted_query()
    {
        await using var db = CreateDb();
        var memberId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        db.Users.AddRange(new User { Id = memberId, UserName = "member" }, new User { Id = otherId, UserName = "other" });
        db.UserClaims.Add(new IdentityUserClaim<Guid>
        {
            UserId = memberId,
            ClaimType = IamTenantScopeResolver.TenantMembershipClaimType,
            ClaimValue = "tenant-a"
        });
        db.AuditLogs.AddRange(
            new AuditLog { UserId = memberId.ToString(), Action = "read" },
            new AuditLog { UserId = otherId.ToString(), Action = "read" });
        await db.SaveChangesAsync();

        var filtered = await db.AuditLogs.WhereTenantActor(db, ["TENANT-A"]).ToListAsync();
        filtered.Should().ContainSingle().Which.UserId.Should().Be(memberId.ToString());
        (await db.AuditLogs.WhereTenantActor(db, []).CountAsync()).Should().Be(2);
        (await db.AuditLogs.WhereTenantActor(db, null).CountAsync()).Should().Be(2);
    }

    [Fact]
    public void Portal_guards_allow_and_deny_each_portal_class_boundary()
    {
        PortalClassGuard.EnsureOperatorPortal(Principal()).Should().BeNull();
        PortalClassGuard.EnsureOperatorPortal(Principal(ConglomerateConstants.PortalClassOperator)).Should().BeNull();
        PortalClassGuard.EnsureOperatorPortal(Principal(ConglomerateConstants.PortalClassCustomerOperator))
            .Should().BeOfType<ForbidHttpResult>();

        PortalClassGuard.EnsureCustomerOperatorPortal(Principal(ConglomerateConstants.PortalClassCustomerOperator)).Should().BeNull();
        PortalClassGuard.EnsureCustomerOperatorPortal(Principal(ConglomerateConstants.PortalClassOperator))
            .Should().BeOfType<ForbidHttpResult>();

        PortalClassGuard.EnsureNotEndUserPortal(Principal()).Should().BeNull();
        PortalClassGuard.EnsureNotEndUserPortal(Principal(ConglomerateConstants.PortalClassEndUser))
            .Should().BeOfType<ForbidHttpResult>();
    }

    [Theory]
    [InlineData("/dashboard")]
    [InlineData("/users")]
    [InlineData("/consents")]
    public void Customer_operator_admin_paths_allow_only_documented_paths(string path)
    {
        var context = new DefaultHttpContext { User = Principal(ConglomerateConstants.PortalClassCustomerOperator) };
        context.Request.Path = path;
        PortalClassGuard.EnsureCustomerOperatorAdminPath(context).Should().BeNull();

        context.Request.Path = "/roles";
        PortalClassGuard.EnsureCustomerOperatorAdminPath(context).Should().BeOfType<ForbidHttpResult>();

        context.User = Principal(ConglomerateConstants.PortalClassOperator);
        PortalClassGuard.EnsureCustomerOperatorAdminPath(context).Should().BeNull();
    }

    private static IdentityDbContext CreateDb() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseInMemoryDatabase($"iam-query-{Guid.NewGuid():N}").Options);

    private static System.Security.Claims.ClaimsPrincipal Principal(string? portalClass = null) =>
        new(new System.Security.Claims.ClaimsIdentity(
            portalClass is null
                ? []
                : [new System.Security.Claims.Claim(ConglomerateConstants.ClaimPortalClass, portalClass)],
            "test"));
}

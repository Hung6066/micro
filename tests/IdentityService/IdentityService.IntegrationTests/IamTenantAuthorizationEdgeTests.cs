using System.Security.Claims;
using FluentAssertions;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests.Authorization;

public sealed class IamTenantAuthorizationEdgeTests
{
    [Fact]
    public void Http_context_filter_round_trips_and_require_fails_closed_when_missing()
    {
        var context = new DefaultHttpContext();
        var filter = new IamTenantScopeFilter([Guid.NewGuid()], ["tenant-a"], false, false);

        IamTenantHttpContext.GetFilter(context).Should().BeNull();
        Action require = () => IamTenantHttpContext.RequireFilter(context);
        require.Should().Throw<InvalidOperationException>();

        IamTenantHttpContext.SetFilter(context, filter);

        IamTenantHttpContext.GetFilter(context).Should().BeSameAs(filter);
        IamTenantHttpContext.RequireFilter(context).Should().BeSameAs(filter);
    }

    [Fact]
    public void Http_context_scope_id_parser_accepts_guid_and_rejects_missing_or_invalid_values()
    {
        var context = new DefaultHttpContext();
        var scopeId = Guid.NewGuid();

        IamTenantHttpContext.ParseScopeId(context.Request).Should().BeNull();
        context.Request.QueryString = new QueryString("?scopeId=not-a-guid");
        IamTenantHttpContext.ParseScopeId(context.Request).Should().BeNull();
        context.Request.QueryString = new QueryString($"?scopeId={scopeId:D}");
        IamTenantHttpContext.ParseScopeId(context.Request).Should().Be(scopeId);
    }

    [Fact]
    public void Scope_and_client_guards_allow_unrestricted_and_reject_forbidden_values()
    {
        var scopeId = Guid.NewGuid();
        var unrestricted = IamTenantScopeFilter.Unrestricted;
        IamTenantAccessGuard.EnsureScopeAccess(scopeId, unrestricted).Should().BeNull();

        var restricted = new IamTenantScopeFilter([scopeId], ["tenant-a"], false, false);
        IamTenantAccessGuard.EnsureScopeAccess(scopeId, restricted).Should().BeNull();
        IamTenantAccessGuard.EnsureScopeAccess(Guid.NewGuid(), restricted)
            .Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>();

        var registry = new DisabledConglomerateTenantRegistry();
        IamTenantAccessGuard.EnsureClientAccess(null, registry, unrestricted).Should().BeNull();
        IamTenantAccessGuard.EnsureClientAccess("client-a", registry, restricted)
            .Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NotFound>();
    }

    [Fact]
    public async Task User_and_role_guards_return_not_found_for_empty_or_unmatched_scope()
    {
        await using var db = CreateDb();
        var filter = new IamTenantScopeFilter([], [], false, false);
        var userResult = await IamTenantAccessGuard.EnsureUserAccessAsync(
            db, Guid.NewGuid(), filter, CancellationToken.None);
        userResult.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NotFound>();

        var roleResult = await IamTenantAccessGuard.EnsureRoleVisibleAsync(
            db, Guid.NewGuid(), filter, CancellationToken.None);
        roleResult.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NotFound>();
    }

    [Fact]
    public async Task Resolver_builds_union_scope_and_supports_requested_subtree()
    {
        await using var db = CreateDb();
        var tenant = new IamScope { Key = "tenant-a", DisplayName = "Tenant A", Kind = "tenant" };
        var organization = new IamScope
        {
            Key = "org-a", DisplayName = "Org A", Kind = "organization", ParentId = tenant.Id
        };
        var environment = new IamScope
        {
            Key = "env-a", DisplayName = "Env A", Kind = "environment", ParentId = organization.Id
        };
        db.IamScopes.AddRange(tenant, organization, environment);
        await db.SaveChangesAsync();

        var user = Principal("tenant-a", "TENANT-A");
        var union = await IamTenantScopeResolver.ResolveAsync(db, user, null, CancellationToken.None);
        union.AccessDenied.Should().BeFalse();
        union.AllowedTenantKeys.Should().ContainSingle().Which.Should().Be("tenant-a");
        union.AllowedScopeIds.Should().Contain(new[] { tenant.Id, organization.Id, environment.Id });

        var requested = await IamTenantScopeResolver.ResolveAsync(db, user, environment.Id, CancellationToken.None);
        requested.AccessDenied.Should().BeFalse();
        requested.AllowedScopeIds.Should().Contain(environment.Id);
        requested.AllowedScopeIds.Should().Contain(organization.Id);
    }

    [Fact]
    public async Task Resolver_denies_unknown_scope_and_hq_uses_unrestricted_mode_when_registry_disabled()
    {
        await using var db = CreateDb();
        db.IamScopes.Add(new IamScope { Key = "tenant-a", DisplayName = "Tenant A", Kind = "tenant" });
        await db.SaveChangesAsync();

        var regular = await IamTenantScopeResolver.ResolveAsync(
            db, Principal("tenant-a"), Guid.NewGuid(), CancellationToken.None);
        regular.AccessDenied.Should().BeTrue();

        var hq = await IamTenantScopeResolver.ResolveAsync(
            db, Principal(IamTenantScopeResolver.GroupHqTenantKey), Guid.NewGuid(),
            new DisabledConglomerateTenantRegistry(), CancellationToken.None);
        hq.Should().Be(IamTenantScopeFilter.Unrestricted);
    }

    [Fact]
    public async Task Resolver_applies_hq_customer_visibility_and_operator_home_rules()
    {
        await using var db = CreateDb();
        var internalTenant = new IamScope { Key = "tenant-internal", DisplayName = "Internal", Kind = "tenant" };
        var customerTenant = new IamScope { Key = "tenant-customer", DisplayName = "Customer", Kind = "tenant" };
        db.IamScopes.AddRange(internalTenant, customerTenant);
        await db.SaveChangesAsync();

        var restrictedRegistry = new TestRegistry(true, "restricted", ["tenant-customer"]);
        var restricted = await IamTenantScopeResolver.ResolveAsync(
            db, Principal(IamTenantScopeResolver.GroupHqTenantKey), null, restrictedRegistry, CancellationToken.None);
        restricted.AllowedTenantKeys.Should().ContainSingle().Which.Should().Be("tenant-internal");

        var operatorHomeAccess = await IamTenantScopeResolver.ResolveAsync(
            db, Principal("tenant-internal"), customerTenant.Id, restrictedRegistry, CancellationToken.None);
        operatorHomeAccess.AccessDenied.Should().BeFalse();
        operatorHomeAccess.AllowedTenantKeys.Should().ContainSingle().Which.Should().Be("tenant-customer");

        var allRegistry = new TestRegistry(true, ConglomerateConstants.HqCustomerVisibilityAll, ["tenant-customer"]);
        var unrestricted = await IamTenantScopeResolver.ResolveAsync(
            db, Principal(IamTenantScopeResolver.GroupHqTenantKey), null, allRegistry, CancellationToken.None);
        unrestricted.Should().Be(IamTenantScopeFilter.Unrestricted);
    }

    private static IdentityDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"iam-edge-{Guid.NewGuid():N}")
            .Options;
        return new IdentityDbContext(options);
    }

    private static ClaimsPrincipal Principal(params string[] memberships) =>
        new(new ClaimsIdentity(
            memberships.Select(value => new Claim(IamTenantScopeResolver.TenantMembershipClaimType, value)),
            "test"));

    private sealed class TestRegistry(bool enabled, string visibility, IReadOnlyList<string> customers) : IConglomerateTenantRegistry
    {
        public bool IsEnabled => enabled;
        public string HqCustomerVisibility => visibility;
        public IReadOnlyList<CrossTenantAllowedPairOptions> AllowedCrossTenantPairs => [];
        public bool IsCustomerTenant(string tenantKey) => customers.Contains(tenantKey, StringComparer.OrdinalIgnoreCase);
        public string? GetOperatorHome(string tenantKey) => IsCustomerTenant(tenantKey) ? "tenant-internal" : null;
        public string GetTenantClass(string tenantKey) => IsCustomerTenant(tenantKey) ? ConglomerateConstants.TenantClassCustomer : ConglomerateConstants.TenantClassInternal;
        public bool IsConglomerateClient(string? clientId) => false;
        public string? GetClientTenant(string? clientId) => null;
        public string GetPortalClass(string? clientId) => ConglomerateConstants.PortalClassOperator;
        public IReadOnlyList<string> GetClientIdsForTenant(string tenantKey) => [];
        public IReadOnlyList<string> GetCustomerTenantsForOperator(string operatorTenantKey) => customers;
        public ConglomerateTenantOptions? GetTenantProfile(string tenantKey) => null;
    }
}

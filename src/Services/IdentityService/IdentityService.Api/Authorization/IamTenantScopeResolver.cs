using System.Security.Claims;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Authorization;

/// <summary>
/// Resolved tenant scope for IAM list/read endpoints.
/// </summary>
public sealed record IamTenantScopeFilter(
    HashSet<Guid>? AllowedScopeIds,
    HashSet<string>? AllowedTenantKeys,
    bool IsGroupHqOperator,
    bool AccessDenied)
{
    public static IamTenantScopeFilter Unrestricted { get; } =
        new(null, null, true, false);

    public bool IsRestricted => AllowedScopeIds is not null || AllowedTenantKeys is not null;
}

public static class IamTenantScopeResolver
{
    public const string TenantMembershipClaimType = "tenant_membership";
    public const string GroupHqTenantKey = "group-hq";

    public static IReadOnlyList<string> GetMemberships(ClaimsPrincipal user) =>
        user.FindAll(TenantMembershipClaimType)
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static bool IsGroupHqOperator(IReadOnlyList<string> memberships) =>
        memberships.Any(membership =>
            string.Equals(membership, GroupHqTenantKey, StringComparison.OrdinalIgnoreCase));

    public static Task<IamTenantScopeFilter> ResolveAsync(
        IdentityDbContext db,
        ClaimsPrincipal user,
        Guid? requestedScopeId,
        CancellationToken ct) =>
        ResolveAsync(db, user, requestedScopeId, registry: null, ct);

    public static async Task<IamTenantScopeFilter> ResolveAsync(
        IdentityDbContext db,
        ClaimsPrincipal user,
        Guid? requestedScopeId,
        IConglomerateTenantRegistry? registry,
        CancellationToken ct)
    {
        var memberships = GetMemberships(user);
        var isHq = IsGroupHqOperator(memberships);
        var scopes = await db.IamScopes.AsNoTracking()
            .Where(scope => scope.IsActive)
            .ToListAsync(ct);

        var tenantScopes = scopes.Where(scope => scope.Kind == "tenant").ToList();
        var allTenantKeys = tenantScopes
            .Select(scope => scope.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var membershipKeys = isHq
            ? SelectHqTenantKeys(allTenantKeys, registry)
            : memberships.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (membershipKeys.Count == 0 && !isHq)
            return Denied();

        // When the conglomerate registry is disabled, the service has no
        // configured customer-tenant boundary to enforce. HQ operators are
        // therefore unrestricted, including for an explicitly requested
        // scope. Keeping this before the requested-scope branch also avoids
        // applying the cross-tenant default-deny policy to the legacy
        // single-tenant deployment mode.
        if (isHq && registry?.IsEnabled != true)
            return IamTenantScopeFilter.Unrestricted;

        if (requestedScopeId is Guid rootScopeId)
        {
            var rootScope = scopes.FirstOrDefault(scope => scope.Id == rootScopeId);
            if (rootScope is null)
                return Denied();

            var tenantScope = FindTenantScope(scopes, rootScope);
            if (tenantScope is null)
                return Denied();

            if (!membershipKeys.Contains(tenantScope.Key) &&
                !CanAccessCustomerTenantViaOperatorHome(memberships, tenantScope.Key, registry))
                return Denied();

            var subtree = CollectSubtreeScopeIds(scopes, rootScopeId, includeOrganizationParent: true);
            return new IamTenantScopeFilter(
                subtree,
                [tenantScope.Key],
                isHq,
                AccessDenied: false);
        }

        if (isHq &&
            registry?.IsEnabled == true &&
            string.Equals(registry.HqCustomerVisibility, ConglomerateConstants.HqCustomerVisibilityAll, StringComparison.OrdinalIgnoreCase))
            return IamTenantScopeFilter.Unrestricted;

        if (isHq && registry?.IsEnabled == true)
            return BuildTenantUnionFilter(scopes, tenantScopes, membershipKeys, isHq);

        if (isHq)
            return IamTenantScopeFilter.Unrestricted;

        return BuildTenantUnionFilter(scopes, tenantScopes, membershipKeys, isHq);
    }

    private static HashSet<string> SelectHqTenantKeys(
        HashSet<string> allTenantKeys,
        IConglomerateTenantRegistry? registry)
    {
        if (registry?.IsEnabled != true)
            return allTenantKeys;

        if (string.Equals(registry.HqCustomerVisibility, ConglomerateConstants.HqCustomerVisibilityAll, StringComparison.OrdinalIgnoreCase))
            return allTenantKeys;

        return allTenantKeys
            .Where(key => !registry.IsCustomerTenant(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool CanAccessCustomerTenantViaOperatorHome(
        IReadOnlyList<string> memberships,
        string targetTenantKey,
        IConglomerateTenantRegistry? registry)
    {
        if (registry?.IsEnabled != true || !registry.IsCustomerTenant(targetTenantKey))
            return false;

        var operatorHome = registry.GetOperatorHome(targetTenantKey);
        return !string.IsNullOrWhiteSpace(operatorHome) &&
               memberships.Any(membership =>
                   string.Equals(membership, operatorHome, StringComparison.OrdinalIgnoreCase));
    }

    private static IamTenantScopeFilter BuildTenantUnionFilter(
        IReadOnlyList<IamScope> scopes,
        IReadOnlyList<IamScope> tenantScopes,
        HashSet<string> allowedTenantKeys,
        bool isHq)
    {
        var allowedScopeIds = new HashSet<Guid>();
        foreach (var tenantKey in allowedTenantKeys)
        {
            var tenantScope = tenantScopes.FirstOrDefault(scope =>
                string.Equals(scope.Key, tenantKey, StringComparison.OrdinalIgnoreCase));
            if (tenantScope is null)
                continue;

            foreach (var scopeId in CollectSubtreeScopeIds(scopes, tenantScope.Id, includeOrganizationParent: true))
                allowedScopeIds.Add(scopeId);
        }

        return new IamTenantScopeFilter(
            allowedScopeIds,
            allowedTenantKeys,
            isHq,
            AccessDenied: false);
    }

    public static IamScope? FindTenantScope(IReadOnlyList<IamScope> scopes, IamScope scope)
    {
        var current = scope;
        while (true)
        {
            if (string.Equals(current.Kind, "tenant", StringComparison.OrdinalIgnoreCase))
                return current;
            if (current.ParentId is not Guid parentId)
                return null;
            var parent = scopes.FirstOrDefault(item => item.Id == parentId);
            if (parent is null)
                return null;
            current = parent;
        }
    }

    public static HashSet<Guid> CollectSubtreeScopeIds(
        IReadOnlyList<IamScope> scopes,
        Guid rootScopeId,
        bool includeOrganizationParent = false)
    {
        var allowed = new HashSet<Guid> { rootScopeId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootScopeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in scopes.Where(scope => scope.ParentId == current))
            {
                if (allowed.Add(child.Id))
                    queue.Enqueue(child.Id);
            }
        }

        if (includeOrganizationParent)
        {
            var root = scopes.FirstOrDefault(scope => scope.Id == rootScopeId);
            if (root?.ParentId is Guid parentId)
                allowed.Add(parentId);
        }

        return allowed;
    }

    private static IamTenantScopeFilter Denied() =>
        new([], [], false, AccessDenied: true);
}

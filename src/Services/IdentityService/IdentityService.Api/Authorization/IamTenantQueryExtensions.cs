using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Authorization;

public static class IamTenantQueryExtensions
{
    public static IQueryable<User> WhereTenantMembership(
        this IQueryable<User> query,
        IApplicationDbContext db,
        HashSet<string>? allowedTenantKeys)
    {
        if (allowedTenantKeys is null || allowedTenantKeys.Count == 0)
            return query;

        var normalizedKeys = allowedTenantKeys
            .Select(key => key.ToLowerInvariant())
            .ToArray();

        return query.Where(user => db.UserClaims.Any(claim =>
            claim.UserId == user.Id &&
            claim.ClaimType == IamTenantScopeResolver.TenantMembershipClaimType &&
            claim.ClaimValue != null && normalizedKeys.Contains(claim.ClaimValue.ToLower())));
    }

    public static IQueryable<User> WhereTenantMembership(
        this IQueryable<User> query,
        IdentityDbContext db,
        HashSet<string>? allowedTenantKeys) =>
        WhereTenantMembership(query, (IApplicationDbContext)db, allowedTenantKeys);

    public static async Task<bool> UserHasTenantAccessAsync(
        IApplicationDbContext db,
        Guid userId,
        HashSet<string>? allowedTenantKeys,
        CancellationToken ct)
    {
        if (allowedTenantKeys is null)
            return true;
        if (allowedTenantKeys.Count == 0)
            return false;

        var normalizedKeys = allowedTenantKeys
            .Select(key => key.ToLowerInvariant())
            .ToArray();

        return await db.UserClaims.AnyAsync(claim =>
            claim.UserId == userId &&
            claim.ClaimType == IamTenantScopeResolver.TenantMembershipClaimType &&
            claim.ClaimValue != null && normalizedKeys.Contains(claim.ClaimValue.ToLower()), ct);
    }

    public static HashSet<string>? ResolveAllowedClientIds(
        IConglomerateTenantRegistry registry,
        IamTenantScopeFilter filter)
    {
        if (filter.AllowedTenantKeys is null)
            return null;

        var clientIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tenantKey in filter.AllowedTenantKeys)
        {
            foreach (var clientId in registry.GetClientIdsForTenant(tenantKey))
                clientIds.Add(clientId);
        }

        return clientIds;
    }

    public static async Task<HashSet<string>?> ResolveTenantPolicyOwnersAsync(
        IApplicationDbContext db,
        IamTenantScopeFilter filter,
        CancellationToken ct)
    {
        if (filter.AllowedScopeIds is null && filter.AllowedTenantKeys is null)
            return null;

        if (filter.AllowedScopeIds is not { Count: > 0 } scopeIds)
            return [];

        var owners = await db.IamResourcePolicies.AsNoTracking()
            .Where(policy => scopeIds.Contains(policy.ScopeId))
            .Select(policy => policy.ServiceKey)
            .Distinct()
            .ToListAsync(ct);

        if (filter.IsGroupHqOperator &&
            (filter.AllowedTenantKeys is null ||
             filter.AllowedTenantKeys.Contains(IamTenantScopeResolver.GroupHqTenantKey, StringComparer.OrdinalIgnoreCase)))
        {
            owners.Add("identity-service");
        }

        return owners.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static IQueryable<AuditLog> WhereTenantActor(
        this IQueryable<AuditLog> query,
        IApplicationDbContext db,
        HashSet<string>? allowedTenantKeys)
    {
        if (allowedTenantKeys is null || allowedTenantKeys.Count == 0)
            return query;

        var normalizedKeys = allowedTenantKeys
            .Select(key => key.ToLowerInvariant())
            .ToArray();

        return query.Where(item =>
            db.Users.Any(user =>
                user.Id.ToString() == item.UserId &&
                db.UserClaims.Any(claim =>
                    claim.UserId == user.Id &&
                    claim.ClaimType == IamTenantScopeResolver.TenantMembershipClaimType &&
                    claim.ClaimValue != null && normalizedKeys.Contains(claim.ClaimValue.ToLower()))));
    }
}

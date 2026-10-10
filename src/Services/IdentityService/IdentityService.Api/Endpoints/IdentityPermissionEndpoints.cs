using System.Security.Claims;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.SharedKernel.Protocol;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

internal static class IdentityPermissionEndpoints
{
    public static void MapIdentityPermissionEndpoints(this RouteGroupBuilder auth, RouteGroupBuilder admin)
    {
        auth.MapGet("/me/permissions", GetEffectivePermissions)
            .RequireAuthorization()
            .WithOpenApi();

        admin.MapGet("/me/permissions", GetAdminEffectivePermissions)
            .RequireAuthorization();
    }

    private static Task<IResult> GetAdminEffectivePermissions(
        HttpContext httpContext,
        UserManager<User> userManager,
        IdentityDbContext db,
        IIdentityService identityService,
        CancellationToken ct) => GetEffectivePermissionsCore(httpContext, userManager, db, identityService, ct, false);

    private static async Task<IResult> GetEffectivePermissions(
        HttpContext httpContext,
        UserManager<User> userManager,
        IdentityDbContext db,
        IIdentityService identityService,
        CancellationToken ct) => await GetEffectivePermissionsCore(httpContext, userManager, db, identityService, ct, true);

    private static async Task<IResult> GetEffectivePermissionsCore(
        HttpContext httpContext,
        UserManager<User> userManager,
        IdentityDbContext db,
        IIdentityService identityService,
        CancellationToken ct,
        bool requireUserClaim)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue(HisHopeProtocolConstants.Claims.Subject);
        if (requireUserClaim && string.IsNullOrWhiteSpace(userId))
            return Results.Unauthorized();

        var scopes = httpContext.User.FindAll("scope")
            .Concat(httpContext.User.FindAll("scp"))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var user = Guid.TryParse(userId, out var parsedUserId)
            ? await userManager.FindByIdAsync(parsedUserId.ToString())
            : null;

        if (user is not null)
        {
            var roles = await userManager.GetRolesAsync(user);
            var facilityIds = await db.UserFacilities
                .Where(membership => membership.UserId == user.Id && membership.IsActive && membership.RevokedAt == null)
                .Select(membership => membership.FacilityId)
                .Distinct()
                .ToArrayAsync(ct);
            var tenantMemberships = (await userManager.GetClaimsAsync(user))
                .Where(claim => claim.Type == HisHopeProtocolConstants.Claims.TenantMembership)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var effectivePermissions = await identityService.GetEffectivePermissionsAsync(user.Id, ct);
            return Results.Ok(new
            {
                userId,
                userName = user.Email ?? user.UserName,
                roles = roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                permissions = effectivePermissions,
                scopes,
                facilityIds,
                tenantId = httpContext.User.FindFirstValue(HisHopeProtocolConstants.Claims.TenantId),
                tenantMemberships,
                authzVersion = user.SecurityStamp
            });
        }

        return Results.Ok(new
        {
            userId,
            userName = httpContext.User.Identity?.Name,
            roles = httpContext.User.FindAll(ClaimTypes.Role).Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            permissions = httpContext.User.FindAll("permission")
                .Concat(httpContext.User.FindAll(HisHopeProtocolConstants.Claims.Permissions))
                .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            scopes,
            facilityIds = httpContext.User.FindAll("facility_ids")
                .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Concat(httpContext.User.FindAll("facility_id").Select(c => c.Value))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            tenantId = httpContext.User.FindFirstValue(HisHopeProtocolConstants.Claims.TenantId),
            tenantMemberships = httpContext.User.FindAll(HisHopeProtocolConstants.Claims.TenantMembership).Select(c => c.Value)
                .Concat(httpContext.User.FindAll("tenant_memberships")
                    .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            authzVersion = httpContext.User.FindFirst("securityVersion")?.Value
        });
    }
}

using System.Security.Claims;
using System.Text.Json;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Audit;
using His.Hope.SharedKernel.Authorization;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class SupportElevationEndpoints
{
    public static RouteGroupBuilder MapSupportElevationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/support-elevations", CreateElevation)
            .RequireAuthorization(AuthorizationConstants.Policies.HumanSuperAdmin)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapGet("/support-elevations", ListElevations)
            .RequireAuthorization(AuthorizationConstants.Policies.HumanSuperAdmin)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapPost("/support-elevations/{id:guid}/approve", ApproveElevation)
            .RequireAuthorization(AuthorizationConstants.Policies.HumanSuperAdmin)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/support-elevations/{id:guid}/revoke", RevokeElevation)
            .RequireAuthorization(AuthorizationConstants.Policies.HumanSuperAdmin)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        return group;
    }

    private static async Task<IResult> CreateElevation(
        SupportElevationCreateRequest request,
        IdentityDbContext db,
        IConglomerateTenantRegistry registry,
        HttpContext http,
        IAuditService audit,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TargetTenant) || request.Reason.Trim().Length < 10)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["targetTenant and a reason of at least 10 characters are required."]
            });

        var sourceTenant = http.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrWhiteSpace(sourceTenant))
            return Results.Forbid();

        if (!registry.IsCustomerTenant(request.TargetTenant))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["targetTenant"] = ["Support elevation target must be a customer tenant."]
            });

        if (!string.Equals(registry.GetOperatorHome(request.TargetTenant), sourceTenant, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sourceTenant, IamTenantScopeResolver.GroupHqTenantKey, StringComparison.OrdinalIgnoreCase))
            return Results.Forbid();

        var operatorUserId = ResolveOperatorUserId(http.User);
        if (operatorUserId == Guid.Empty)
            return Results.Forbid();

        var permissions = (request.Permissions ?? ["admin.users.write", "identity.update"])
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var elevation = new SupportElevation
        {
            OperatorUserId = operatorUserId,
            SourceTenant = sourceTenant,
            TargetTenant = request.TargetTenant.Trim(),
            PermissionsJson = JsonSerializer.Serialize(permissions),
            Status = "pending",
            RequestedBy = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub"),
            Reason = request.Reason.Trim(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(Math.Clamp(request.DurationMinutes, 5, 60))
        };

        db.SupportElevations.Add(elevation);
        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAsync(audit, http, "SUPPORT_ELEVATION_REQUEST", "SupportElevation", elevation.Id.ToString("D"), ct);

        return Results.Created($"/api/v1/admin/support-elevations/{elevation.Id:D}", new
        {
            elevation.Id,
            elevation.SourceTenant,
            elevation.TargetTenant,
            elevation.Status,
            elevation.ExpiresAt,
            permissions
        });
    }

    private static async Task<IResult> ApproveElevation(
        Guid id,
        IdentityDbContext db,
        IAuditService audit,
        HttpContext http,
        CancellationToken ct)
    {
        var elevation = await db.SupportElevations.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (elevation is null) return Results.NotFound();
        if (elevation.Status != "pending" || elevation.ExpiresAt <= DateTime.UtcNow)
            return Results.Conflict(new { errorCode = "support_elevation_not_pending" });

        var approver = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(approver)) return Results.Unauthorized();
        if (string.Equals(elevation.RequestedBy, approver, StringComparison.OrdinalIgnoreCase))
            return Results.Conflict(new { errorCode = "maker_checker_conflict" });

        elevation.Status = "approved";
        elevation.ApprovedBy = approver;
        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAsync(audit, http, "SUPPORT_ELEVATION_APPROVE", "SupportElevation", id.ToString("D"), ct);
        return Results.Ok(new { elevation.Id, elevation.Status, elevation.ApprovedBy, elevation.ExpiresAt });
    }

    private static async Task<IResult> RevokeElevation(
        Guid id,
        IdentityDbContext db,
        IAuditService audit,
        HttpContext http,
        CancellationToken ct)
    {
        var elevation = await db.SupportElevations.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (elevation is null) return Results.NotFound();
        if (elevation.Status != "approved")
            return Results.Conflict(new { errorCode = "support_elevation_not_active" });

        elevation.Status = "revoked";
        elevation.ExpiresAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAsync(audit, http, "SUPPORT_ELEVATION_REVOKE", "SupportElevation", id.ToString("D"), ct);
        return Results.Ok(new { elevation.Id, elevation.Status });
    }

    private static async Task<IResult> ListElevations(
        IdentityDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        _ = IamTenantHttpContext.RequireFilter(http);
        var operatorUserId = ResolveOperatorUserId(http.User);
        var items = await db.SupportElevations.AsNoTracking()
            .Where(item => item.OperatorUserId == operatorUserId && item.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(item => item.CreatedAt)
            .Take(50)
            .Select(item => new
            {
                item.Id,
                item.SourceTenant,
                item.TargetTenant,
                item.Status,
                item.Reason,
                item.ExpiresAt,
                item.CreatedAt
            })
            .ToListAsync(ct);
        return Results.Ok(items);
    }

    private static Guid ResolveOperatorUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subject, out var userId) ? userId : Guid.Empty;
    }

    public sealed record SupportElevationCreateRequest(
        string TargetTenant,
        string Reason,
        int DurationMinutes = 30,
        IReadOnlyCollection<string>? Permissions = null);
}

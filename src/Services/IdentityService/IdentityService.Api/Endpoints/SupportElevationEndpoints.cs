using System.Security.Claims;
using System.Text.Json;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.SharedKernel.Authorization;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class SupportElevationEndpoints
{
    public static RouteGroupBuilder MapSupportElevationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/support-elevations", CreateElevation)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapGet("/support-elevations", ListElevations)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        return group;
    }

    private static async Task<IResult> CreateElevation(
        SupportElevationCreateRequest request,
        IdentityDbContext db,
        IConglomerateTenantRegistry registry,
        HttpContext http,
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
            Status = "approved",
            RequestedBy = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub"),
            ApprovedBy = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub"),
            Reason = request.Reason.Trim(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(Math.Clamp(request.DurationMinutes, 5, 60))
        };

        db.SupportElevations.Add(elevation);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/v1/admin/support-elevations/{elevation.Id:D}", new
        {
            elevation.Id,
            elevation.SourceTenant,
            elevation.TargetTenant,
            elevation.ExpiresAt,
            permissions
        });
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

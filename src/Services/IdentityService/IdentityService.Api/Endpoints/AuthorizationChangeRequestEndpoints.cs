using System.Security.Claims;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.SharedKernel.Authorization;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// Shared four-eyes workflow for high-risk authorization mutations. Resource
/// endpoints remain responsible for validating and executing their own
/// snapshots; this endpoint owns request state, SoD and approval evidence.
/// </summary>
public static class AuthorizationChangeRequestEndpoints
{
    private static readonly IReadOnlyDictionary<string, string> SupportedResourceActions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Role:role.publish"] = "Role",
        ["Role:role.rollback"] = "Role",
        ["AuthorizationPolicy:policy.publish"] = "AuthorizationPolicy",
        ["AuthorizationPolicy:policy.rollback"] = "AuthorizationPolicy"
    };

    public static RouteGroupBuilder MapAuthorizationChangeRequestEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/authorization-change-requests", List)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);
        group.MapPost("/authorization-change-requests", Create)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();
        group.MapPost("/authorization-change-requests/{id:guid}/approve", Approve)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();
        group.MapPost("/authorization-change-requests/{id:guid}/reject", Reject)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();
        return group;
    }

    private static async Task<IResult> List(
        IApplicationDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        _ = IamTenantHttpContext.RequireFilter(http);
        var items = await db.AuthorizationChangeRequests.AsNoTracking()
            .Where(item => item.ExpiresAt > DateTime.UtcNow || item.Status == "pending")
            .OrderByDescending(item => item.RequestedAt)
            .Take(200)
            .Select(item => new
            {
                item.Id,
                item.ResourceType,
                item.ResourceId,
                item.Action,
                item.RequestedBy,
                item.Reason,
                item.Status,
                item.ApprovedBy,
                item.RequestedAt,
                item.DecidedAt,
                item.ExecutedAt,
                item.ExpiresAt
            })
            .ToListAsync(ct);
        return Results.Ok(items);
    }

    private static async Task<IResult> Create(
        AuthorizationChangeRequestCreateRequest request,
        IApplicationDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp) return stepUp;
        var resourceType = request.ResourceType?.Trim() ?? string.Empty;
        var action = request.Action?.Trim().ToLowerInvariant() ?? string.Empty;
        if (request.ResourceId == Guid.Empty ||
            !SupportedResourceActions.ContainsKey($"{resourceType}:{action}") ||
            request.Reason.Trim().Length < 10)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["resourceType, resourceId, supported action and a reason of at least 10 characters are required."]
            });

        var actor = AuthorizationChangeRequestWorkflow.Actor(http);
        var existing = await db.AuthorizationChangeRequests.FirstOrDefaultAsync(item =>
            item.ResourceId == request.ResourceId &&
            item.ResourceType == resourceType &&
            item.Action == action &&
            item.Status == "pending" && item.ExpiresAt > DateTime.UtcNow, ct);
        if (existing is not null)
            return Results.Conflict(new { errorCode = "authorization_change_already_pending", id = existing.Id });

        var item = new AuthorizationChangeRequest
        {
            ResourceType = resourceType,
            ResourceId = request.ResourceId,
            Action = action,
            RequestedBy = actor,
            Reason = request.Reason.Trim(),
            PayloadJson = string.IsNullOrWhiteSpace(request.PayloadJson) ? "{}" : request.PayloadJson,
            ExpiresAt = DateTime.UtcNow.AddHours(Math.Clamp(request.ExpiryHours, 1, 72))
        };
        db.AuthorizationChangeRequests.Add(item);
        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAuthorizationChangeAsync(db, http, "CHANGE_REQUEST_CREATE",
            item.ResourceType, item.ResourceId.ToString("D"), item.Reason, null, item.PayloadJson, ct);
        return Results.Created($"/api/v1/admin/authorization-change-requests/{item.Id:D}", ToResponse(item));
    }

    private static async Task<IResult> Approve(
        Guid id,
        IApplicationDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp) return stepUp;
        var item = Guard.Against.NotFound(
            await db.AuthorizationChangeRequests.FirstOrDefaultAsync(value => value.Id == id, ct), "AuthorizationChangeRequest", id);
        if (item.Status != "pending" || item.ExpiresAt <= DateTime.UtcNow)
            return Results.Conflict(new { errorCode = "authorization_change_not_pending" });
        var approver = AuthorizationChangeRequestWorkflow.Actor(http);
        if (string.Equals(item.RequestedBy, approver, StringComparison.OrdinalIgnoreCase))
            return Results.Conflict(new { errorCode = "maker_checker_conflict" });
        item.Status = "approved";
        item.ApprovedBy = approver;
        item.DecidedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAuthorizationChangeAsync(db, http, "CHANGE_REQUEST_APPROVE",
            item.ResourceType, item.ResourceId.ToString("D"), item.Reason, null, item.PayloadJson, ct);
        return Results.Ok(ToResponse(item));
    }

    private static async Task<IResult> Reject(
        Guid id,
        IApplicationDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp) return stepUp;
        var item = Guard.Against.NotFound(
            await db.AuthorizationChangeRequests.FirstOrDefaultAsync(value => value.Id == id, ct), "AuthorizationChangeRequest", id);
        if (item.Status != "pending" || item.ExpiresAt <= DateTime.UtcNow)
            return Results.Conflict(new { errorCode = "authorization_change_not_pending" });
        var actor = AuthorizationChangeRequestWorkflow.Actor(http);
        item.Status = "rejected";
        item.ApprovedBy = actor;
        item.DecidedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAuthorizationChangeAsync(db, http, "CHANGE_REQUEST_REJECT",
            item.ResourceType, item.ResourceId.ToString("D"), item.Reason, item.PayloadJson, null, ct);
        return Results.Ok(ToResponse(item));
    }

    private static object ToResponse(AuthorizationChangeRequest item) => new
    {
        item.Id,
        item.ResourceType,
        item.ResourceId,
        item.Action,
        item.RequestedBy,
        item.Reason,
        item.Status,
        item.ApprovedBy,
        item.RequestedAt,
        item.DecidedAt,
        item.ExecutedAt,
        item.ExpiresAt
    };

    public sealed record AuthorizationChangeRequestCreateRequest(
        string ResourceType,
        Guid ResourceId,
        string Action,
        string Reason,
        string? PayloadJson = null,
        int ExpiryHours = 24);
}

internal static class AuthorizationChangeRequestWorkflow
{
    internal static string Actor(HttpContext http) =>
        http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject) ?? "unknown";

    internal static bool TryGetRequestId(HttpContext http, out Guid requestId)
    {
        var value = http.Request.Query["changeRequestId"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            requestId = Guid.Empty;
            return false;
        }
        return Guid.TryParse(value, out requestId);
    }

    internal static Task<AuthorizationChangeRequest?> FindApprovedAsync(
        IApplicationDbContext db,
        Guid requestId,
        string resourceType,
        Guid resourceId,
        string action,
        string approver,
        CancellationToken ct) => db.AuthorizationChangeRequests.FirstOrDefaultAsync(item =>
            item.Id == requestId && item.ResourceType == resourceType && item.ResourceId == resourceId &&
            item.Action == action && item.Status == "approved" && item.ApprovedBy == approver &&
            item.ExpiresAt > DateTime.UtcNow, ct);

    internal static async Task<AuthorizationChangeRequest> CreatePendingAsync(
        IApplicationDbContext db,
        HttpContext http,
        string resourceType,
        Guid resourceId,
        string action,
        string payloadJson,
        string reason,
        CancellationToken ct)
    {
        var actor = Actor(http);
        var existing = await db.AuthorizationChangeRequests.FirstOrDefaultAsync(item =>
            item.ResourceType == resourceType && item.ResourceId == resourceId && item.Action == action &&
            item.Status == "pending" && item.RequestedBy == actor && item.ExpiresAt > DateTime.UtcNow, ct);
        if (existing is not null) return existing;

        var item = new AuthorizationChangeRequest
        {
            ResourceType = resourceType,
            ResourceId = resourceId,
            Action = action,
            RequestedBy = actor,
            Reason = reason,
            PayloadJson = payloadJson,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
        db.AuthorizationChangeRequests.Add(item);
        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAuthorizationChangeAsync(db, http, "CHANGE_REQUEST_CREATE",
            resourceType, resourceId.ToString("D"), reason, null, payloadJson, ct);
        return item;
    }

    internal static async Task MarkExecutedAsync(
        IApplicationDbContext db,
        AuthorizationChangeRequest request,
        HttpContext http,
        CancellationToken ct)
    {
        request.Status = "executed";
        request.ExecutedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAuthorizationChangeAsync(db, http, "CHANGE_REQUEST_EXECUTE",
            request.ResourceType, request.ResourceId.ToString("D"), request.Reason,
            request.PayloadJson, null, ct);
    }
}

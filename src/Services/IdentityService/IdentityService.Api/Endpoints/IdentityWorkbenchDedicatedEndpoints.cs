using System.Security.Claims;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.Infrastructure.Audit;
using His.Hope.Infrastructure.Security;
using His.Hope.SharedKernel.Authorization;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// Dedicated Identity Workbench resource handlers. These routes are intentionally
/// separate from the legacy aggregate projections so each menu has a stable API
/// boundary, permission and audit contract.
/// </summary>
public static class IdentityWorkbenchDedicatedEndpoints
{
    public static void MapIdentityWorkbenchDedicatedEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/workload-sessions", async (IdentityDbContext db, IWorkloadSessionStore sessions, CancellationToken ct) =>
        {
            var roles = await db.IamWorkloadRoles.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Key).ToListAsync(ct);
            var rows = new List<object>();
            foreach (var role in roles)
            {
                foreach (var session in await sessions.ListAsync(role.Audience, ct))
                    rows.Add(new { workloadRoleId = role.Id, workloadRoleKey = role.Key, audience = role.Audience, sessionId = session.SessionId, issuedAt = session.IssuedAt, expiresAt = session.ExpiresAt, active = session.ExpiresAt > DateTime.UtcNow });
            }
            return Results.Ok(new { schemaVersion = "iam-workload-sessions.v1", evaluatedAt = DateTime.UtcNow, sessions = rows });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead);

        group.MapDelete("/workload-sessions/{workloadRoleId:guid}/{sessionId}", async (Guid workloadRoleId, string sessionId, IdentityDbContext db, IWorkloadSessionStore sessions, HttpContext http, CancellationToken ct) =>
        {
            var role = await db.IamWorkloadRoles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == workloadRoleId, ct);
            if (role is null) return Results.NotFound("workload_role_not_found");
            var revoked = await sessions.RevokeAsync(role.Audience, sessionId, ct);
            if (!revoked) return Results.NotFound("workload_session_not_found");
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_WORKLOAD_SESSION_REVOKE", "IamWorkloadSession", $"{workloadRoleId:D}:{sessionId}", "Workload session revoked.", sessionId, "revoked", ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite);

        group.MapGet("/revocations", async (IdentityDbContext db, int? limit, CancellationToken ct) =>
        {
            var take = Math.Clamp(limit ?? 100, 1, 500);
            var items = await db.AuditLogs.AsNoTracking()
                .Where(x => x.Action.Contains("REVOKE") || x.Action.Contains("REVOCATION"))
                .OrderByDescending(x => x.Timestamp).Take(take)
                .Select(x => new { id = x.Id, action = x.Action, resourceType = x.ResourceType, resourceId = x.ResourceId, userId = x.UserId, reason = x.Details, occurredAt = x.Timestamp, correlationId = x.CorrelationId })
                .ToListAsync(ct);
            return Results.Ok(new { schemaVersion = "iam-revocations.v1", evaluatedAt = DateTime.UtcNow, revocations = items });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSessionsRead);

        group.MapPost("/revocations", async (RevocationRequest request, IdentityDbContext db, ITokenBlacklistService blacklist, ClaimsPrincipal actor, HttpContext http, CancellationToken ct) =>
        {
            if (request.PrincipalId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["revocation"] = ["principalId and reason are required."] });
            var principal = request.PrincipalType?.Trim().ToLowerInvariant() ?? "human";
            if (principal is not ("human" or "workload")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["principalType"] = ["principalType must be human or workload."] });
            var subject = request.PrincipalId.ToString("D");
            if (principal == "workload")
            {
                var role = await db.IamWorkloadRoles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.PrincipalId, ct);
                if (role is null) return Results.NotFound("workload_role_not_found");
                subject = role.Audience;
            }
            else if (!await db.Users.AnyAsync(x => x.Id == request.PrincipalId, ct)) return Results.NotFound("user_not_found");
            await blacklist.RevokeAllUserTokensAsync(subject, ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "IAM_REVOCATION_CREATE", "IamRevocation", request.PrincipalId.ToString("D"), request.Reason.Trim(), null, JsonSerializer.Serialize(new { principalType = principal, subject, actor = actor.FindFirstValue("sub") }), ct);
            return Results.Ok(new { schemaVersion = "iam-revocation.v1", principalId = request.PrincipalId, principalType = principal, subject, revokedAt = DateTime.UtcNow });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSessionsRevoke);

        group.MapGet("/analyzer/effective-access/{principalId:guid}", async (Guid principalId, IdentityDbContext db, CancellationToken ct) =>
        {
            var exists = await db.Users.AsNoTracking().AnyAsync(x => x.Id == principalId && x.IsActive, ct) || await db.IamWorkloadRoles.AsNoTracking().AnyAsync(x => x.Id == principalId && x.IsActive, ct);
            if (!exists) return Results.NotFound("principal_not_found");
            var assignments = await db.IamPermissionSetAssignments.AsNoTracking().Where(x => x.PrincipalId == principalId && x.Status == "active" && (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow)).Join(db.IamPermissionSets.Where(x => x.LifecycleStatus == AuthorizationConstants.LifecycleStatuses.Published), x => x.PermissionSetId, x => x.Id, (_, set) => set.PermissionsJson).ToListAsync(ct);
            var permissions = assignments.SelectMany(x => JsonSerializer.Deserialize<string[]>(x) ?? []).Distinct(StringComparer.Ordinal).Order().ToArray();
            return Results.Ok(new { schemaVersion = "iam-effective-access.v1", principalId, permissions, evaluatedAt = DateTime.UtcNow });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminPolicySimulate);

        group.MapPost("/analyzer/policy-simulator", async (PolicySimulationRequest request, IdentityDbContext db, CancellationToken ct) =>
        {
            if (request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.PermissionCode)) return Results.BadRequest("userId and permissionCode are required");
            var effective = await db.IamPermissionSetAssignments.AsNoTracking().Where(x => x.PrincipalId == request.UserId && x.Status == "active" && (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow)).Join(db.IamPermissionSets.Where(x => x.LifecycleStatus == AuthorizationConstants.LifecycleStatuses.Published), x => x.PermissionSetId, x => x.Id, (_, set) => set.PermissionsJson).ToListAsync(ct);
            var allowed = effective.SelectMany(x => JsonSerializer.Deserialize<string[]>(x) ?? []).Contains(request.PermissionCode.Trim().ToLowerInvariant(), StringComparer.Ordinal);
            return Results.Ok(new { schemaVersion = "iam-policy-simulation.v1", request.UserId, permissionCode = request.PermissionCode.Trim().ToLowerInvariant(), allowed, evaluatedAt = DateTime.UtcNow });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminPolicySimulate);

        group.MapPost("/analyzer/access-diff", (AccessDiffRequest request) =>
        {
            var before = (request.Before ?? []).Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).ToHashSet(StringComparer.Ordinal);
            var after = (request.After ?? []).Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).ToHashSet(StringComparer.Ordinal);
            return Results.Ok(new { schemaVersion = "iam-access-diff.v1", added = after.Except(before).Order().ToArray(), removed = before.Except(after).Order().ToArray(), unchanged = before.Intersect(after).Order().ToArray(), evaluatedAt = DateTime.UtcNow });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminPolicySimulate);

        group.MapGet("/analyzer/unused-permissions", async (IdentityDbContext db, CancellationToken ct) =>
        {
            var used = await db.RolePermissions.AsNoTracking().Select(x => x.PermissionCode).Distinct().ToListAsync(ct);
            var sets = await db.IamPermissionSets.AsNoTracking().Where(x => x.LifecycleStatus == AuthorizationConstants.LifecycleStatuses.Published).Select(x => x.PermissionsJson).ToListAsync(ct);
            used.AddRange(sets.SelectMany(x => JsonSerializer.Deserialize<string[]>(x) ?? []));
            var unused = HisHopePermissions.All.Except(used, StringComparer.Ordinal).Order().ToArray();
            return Results.Ok(new { schemaVersion = "iam-unused-permissions.v1", unusedPermissions = unused, count = unused.Length, evaluatedAt = DateTime.UtcNow });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminPolicySimulate);

        group.MapGet("/audit-integrations", async (IdentityDbContext db, IConfiguration configuration, CancellationToken ct) =>
        {
            var pendingSignals = await db.SecuritySignalOutbox.CountAsync(x => x.DispatchedAt == null, ct);
            var lastAudit = await db.AuditLogs.AsNoTracking().OrderByDescending(x => x.Timestamp).Select(x => (DateTime?)x.Timestamp).FirstOrDefaultAsync(ct);
            return Results.Ok(new { schemaVersion = "iam-audit-integrations.v1", evaluatedAt = DateTime.UtcNow, audit = new { appendOnly = configuration.GetValue("AUDIT_APPEND_ONLY", true), redactionEnabled = configuration.GetValue("AUDIT_REDACTION_ENABLED", true), lastEventAt = lastAudit }, ssf = new { enabled = configuration.GetValue("SSF_ENABLED", configuration.GetValue("SecuritySignals:Enabled", false)), pending = pendingSignals } });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsRead);
    }

    public sealed record RevocationRequest(Guid PrincipalId, string? PrincipalType, string Reason);
    public sealed record PolicySimulationRequest(Guid UserId, string PermissionCode);
    public sealed record AccessDiffRequest(IReadOnlyCollection<string>? Before, IReadOnlyCollection<string>? After);
}

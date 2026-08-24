using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Infrastructure.Facility;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Infrastructure.Security;
using His.Hope.Infrastructure.Audit;
using His.Hope.SharedKernel.Authorization;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>Administrative incident-response controls. Secrets and token values never leave the service.</summary>
public static class AdminIncidentEndpoints
{
    public static void MapAdminIncidentEndpoints(this WebApplication app)
    {
        MapAdminIncidentEndpoints(app.MapGroup("/api/v1/admin")
            .RequireAuthorization(AuthorizationConstants.Policies.HumanAdmin));
    }

    public static void MapAdminIncidentEndpoints(this RouteGroupBuilder group)
    {

        group.MapGet("/sessions", async (
            IdentityDbContext db,
            IUserSessionTracker sessionTracker,
            IConnectionMultiplexer redis,
            FacilityContext facility,
            HttpContext http,
            CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var users = await db.Users.AsNoTracking()
                .Where(userEntity => userEntity.IsActive)
                .WhereTenantMembership(db, filter.AllowedTenantKeys)
                .OrderBy(userEntity => userEntity.Email)
                .Take(1000)
                .Select(userEntity => new { userEntity.Id, userEntity.Email, userEntity.UserName })
                .ToListAsync(ct);
            var database = redis.GetDatabase();
            var sessions = new List<object>();
            foreach (var tenantUser in users)
            {
                if (!await HasFacilityAccessAsync(db, facility, tenantUser.Id, ct)) continue;
                foreach (var sessionId in await sessionTracker.GetUserSessionsAsync(tenantUser.Id.ToString()))
                {
                    var raw = await database.StringGetAsync($"session:{sessionId}");
                    var session = raw.HasValue ? JsonSerializer.Deserialize<SessionData>(raw!) : null;
                    sessions.Add(new
                    {
                        userId = tenantUser.Id,
                        email = tenantUser.Email ?? tenantUser.UserName,
                        id = sessionId,
                        deviceInfo = session?.UserAgentHash is { Length: > 0 } hash ? hash[..Math.Min(20, hash.Length)] : null,
                        issuedAt = session?.IssuedAt,
                        expiresAt = session?.ExpiresAt,
                        active = session is not null && !session.IsExpired
                    });
                }
            }
            return Results.Ok(new { schemaVersion = "admin-session-center.v1", evaluatedAt = DateTime.UtcNow, sessions });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSessionsRead)
            .WithTenantReadScope(HisHopePermissions.Admin.SessionsRead);

        group.MapGet("/users/{id:guid}/sessions", async (
            Guid id,
            IdentityDbContext db,
            IUserSessionTracker sessionTracker,
            IConnectionMultiplexer redis,
            FacilityContext facility,
            HttpContext http,
            CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, id, filter, ct) is { } accessError)
                return accessError;
            if (!await HasFacilityAccessAsync(db, facility, id, ct)) return Results.Forbid();
            if (!await db.Users.AnyAsync(userEntity => userEntity.Id == id, ct)) return Results.NotFound();

            var current = await sessionTracker.GetUserSessionsAsync(id.ToString());
            var database = redis.GetDatabase();
            var sessions = new List<object>(current.Length);
            foreach (var sessionId in current)
            {
                var raw = await database.StringGetAsync($"session:{sessionId}");
                var session = raw.HasValue ? JsonSerializer.Deserialize<SessionData>(raw!) : null;
                sessions.Add(new
                {
                    id = sessionId,
                    deviceInfo = session?.UserAgentHash is { Length: > 0 } hash ? hash[..Math.Min(20, hash.Length)] : null,
                    issuedAt = session?.IssuedAt,
                    expiresAt = session?.ExpiresAt,
                    active = session is not null && !session.IsExpired
                });
            }

            return Results.Ok(new { userId = id, sessions });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSessionsRead)
            .WithTenantReadScope(HisHopePermissions.Admin.SessionsRead);

        group.MapDelete("/users/{id:guid}/sessions/{sessionId}", async (
            Guid id,
            string sessionId,
            string? reason,
            IdentityDbContext db,
            IUserSessionTracker sessionTracker,
            IConnectionMultiplexer redis,
            FacilityContext facility,
            IAuditService audit,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(reason)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["A reason is required."] });
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, id, filter, ct) is { } accessError)
                return accessError;
            if (!await HasFacilityAccessAsync(db, facility, id, ct)) return Results.Forbid();
            var sessionIds = await sessionTracker.GetUserSessionsAsync(id.ToString());
            if (!sessionIds.Contains(sessionId, StringComparer.Ordinal)) return Results.NotFound();

            await redis.GetDatabase().KeyDeleteAsync($"session:{sessionId}");
            await sessionTracker.RemoveSessionAsync(id.ToString(), sessionId);
            await AdminAudit.LogAsync(audit, http, "REVOKE_SESSION", "UserSession", $"{id}:{sessionId}", ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSessionsRevoke)
            .WithTenantMutationScope();

        group.MapPost("/users/{id:guid}/sessions/revoke-all", async (
            Guid id,
            AdminRevokeAllRequest request,
            IdentityDbContext db,
            IUserSessionTracker sessionTracker,
            IConnectionMultiplexer redis,
            ITokenBlacklistService tokenBlacklist,
            FacilityContext facility,
            IAuditService audit,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["A reason is required."] });
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, id, filter, ct) is { } accessError)
                return accessError;
            if (!await HasFacilityAccessAsync(db, facility, id, ct)) return Results.Forbid();
            if (!await db.Users.AnyAsync(userEntity => userEntity.Id == id, ct)) return Results.NotFound();

            var sessionIds = await sessionTracker.GetUserSessionsAsync(id.ToString());
            var keys = sessionIds.Select(sessionId => (RedisKey)$"session:{sessionId}").ToArray();
            if (keys.Length > 0) await redis.GetDatabase().KeyDeleteAsync(keys);
            await sessionTracker.ClearUserSessionsAsync(id.ToString());
            await tokenBlacklist.RevokeAllUserTokensAsync(id.ToString(), ct);
            await AdminAudit.LogAsync(audit, http, "REVOKE_ALL_SESSIONS", "User", id.ToString(), ct);
            return Results.Ok(new { userId = id, revokedSessions = sessionIds.Length });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSessionsRevoke)
            .WithTenantMutationScope();

        group.MapPost("/users/{id:guid}/credentials/reset", async (
            Guid id,
            AdminCredentialResetRequest request,
            IdentityDbContext db,
            ITokenBlacklistService tokenBlacklist,
            FacilityContext facility,
            IAuditService audit,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["A reason is required."] });
            if (!request.ResetMfa && !request.RevokePasskeys) return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Select MFA or passkeys to reset."] });
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, id, filter, ct) is { } userError) return userError;
            if (!await HasFacilityAccessAsync(db, facility, id, ct)) return Results.Forbid();
            var user = await db.Users.SingleOrDefaultAsync(item => item.Id == id, ct);
            if (user is null) return Results.NotFound();

            var removedMfa = 0;
            var removedPasskeys = 0;
            if (request.ResetMfa)
            {
                removedMfa = await db.UserMfas.Where(item => item.UserId == id).ExecuteDeleteAsync(ct);
                user.TwoFactorEnabled = false;
            }
            if (request.RevokePasskeys)
                removedPasskeys = await db.PasskeyCredentials.Where(item => item.UserId == id.ToString()).ExecuteDeleteAsync(ct);

            await db.SaveChangesAsync(ct);
            await tokenBlacklist.RevokeAllUserTokensAsync(id.ToString(), ct);
            await AdminAudit.LogAsync(audit, http, "RESET_CREDENTIALS", "User", id.ToString(), ct);
            return Results.Ok(new { userId = id, removedMfa, removedPasskeys, tokensRevoked = true });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminCredentialsReset)
            .WithTenantMutationScope();
    }

    private static async Task<bool> HasFacilityAccessAsync(IdentityDbContext db, FacilityContext facility, Guid userId, CancellationToken ct)
    {
        if (facility.IsCrossFacility) return true;
        var allowed = facility.AuthorizedFacilities.Append(facility.FacilityId)
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return allowed.Length == 0 || await db.UserFacilities.AnyAsync(item => item.UserId == userId && item.IsActive && item.RevokedAt == null && allowed.Contains(item.FacilityId), ct);
    }

    public sealed record AdminRevokeAllRequest(string Reason);
    public sealed record AdminCredentialResetRequest(bool ResetMfa, bool RevokePasskeys, string Reason);
}

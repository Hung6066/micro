using System.Security.Claims;
using System.Text.Json;
using His.Hope.IdentityService.Application.DevicePosture;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Facility;
using His.Hope.Contracts.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class DevicePostureEndpoints
{
    public static void MapDevicePostureEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup(IdentityApiRoutes.AdminDevicePosture)
            .RequireAuthorization(AuthorizationConstants.Policies.HumanAdmin);
        admin.MapGet("/policy", async (IdentityDbContext db, IConfiguration configuration, CancellationToken ct) =>
        {
            var policy = await GetPolicyAsync(db, configuration, ct);
            return Results.Ok(ToPolicyResponse(policy));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsRead);

        admin.MapPut("/policy", async (DevicePosturePolicyInput request, IdentityDbContext db, IConfiguration configuration, HttpContext http, CancellationToken ct) =>
        {
            var mode = request.Mode.Trim().ToLowerInvariant();
            if (mode is not ("observe" or "stepup" or "deny")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["mode"] = ["Mode must be observe, stepup or deny."] });
            if (request.EvidenceTtlSeconds is < 60 or > 3600) return Results.ValidationProblem(new Dictionary<string, string[]> { ["evidenceTtlSeconds"] = ["TTL must be between 60 and 3600 seconds."] });
            var providers = request.Providers.Select(value => value.Trim().ToLowerInvariant()).Distinct().ToArray();
            if (providers.Any(provider => !DevicePostureProviders.Allowed.Contains(provider))) return Results.ValidationProblem(new Dictionary<string, string[]> { ["providers"] = ["Unknown posture provider."] });
            var signals = request.RequiredSignals.Select(value => value.Trim().ToLowerInvariant()).Where(value => value.Length > 0).Distinct().ToArray();
            if (signals.Any(value => value.Length > 64)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["requiredSignals"] = ["Signal names are limited to 64 characters."] });
            var policy = await GetPolicyAsync(db, configuration, ct);
            var beforeJson = JsonSerializer.Serialize(ToPolicyResponse(policy));
            policy.Mode = mode;
            policy.ProvidersJson = JsonSerializer.Serialize(providers);
            policy.RequiredSignalsJson = JsonSerializer.Serialize(signals);
            policy.EvidenceTtlSeconds = request.EvidenceTtlSeconds;
            policy.Version = (int.TryParse(policy.Version, out var version) ? version + 1 : 1).ToString();
            policy.UpdatedAt = DateTime.UtcNow;
            policy.UpdatedBy = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub");
            await db.SaveChangesAsync(ct);
            WriteAudit(db, http, "UPDATE", "DevicePosturePolicy", policy.Id, beforeJson, JsonSerializer.Serialize(ToPolicyResponse(policy)));
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToPolicyResponse(policy));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsWrite);

        admin.MapPost("/policy/rollback", async (IdentityDbContext db, IConfiguration configuration, HttpContext http, CancellationToken ct) =>
        {
            if (!http.User.FindAll("amr").Any(claim => claim.Value.Equals("mfa", StringComparison.OrdinalIgnoreCase)))
                return Results.Forbid();
            var policy = await GetPolicyAsync(db, configuration, ct);
            var previous = await db.AuditLogs.AsNoTracking()
                .Where(item => item.Source == "device-posture" && item.ResourceType == "DevicePosturePolicy" && item.Action == "UPDATE" && item.BeforeJson != null)
                .OrderByDescending(item => item.Timestamp)
                .Select(item => item.BeforeJson)
                .FirstOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(previous)) return Results.Conflict(new { errorCode = "no_previous_policy" });
            var prior = JsonSerializer.Deserialize<DevicePosturePolicySnapshot>(previous);
            if (prior is null) return Results.Conflict(new { errorCode = "invalid_previous_policy" });
            var beforeJson = JsonSerializer.Serialize(ToPolicyResponse(policy));
            policy.Mode = prior.Mode;
            policy.ProvidersJson = JsonSerializer.Serialize(prior.Providers);
            policy.RequiredSignalsJson = JsonSerializer.Serialize(prior.RequiredSignals);
            policy.EvidenceTtlSeconds = prior.EvidenceTtlSeconds;
            policy.Version = (int.TryParse(policy.Version, out var version) ? version + 1 : 1).ToString();
            policy.UpdatedAt = DateTime.UtcNow;
            policy.UpdatedBy = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub");
            WriteAudit(db, http, "ROLLBACK", "DevicePosturePolicy", policy.Id, beforeJson, JsonSerializer.Serialize(ToPolicyResponse(policy)));
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToPolicyResponse(policy));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsWrite);

        admin.MapPost("/assessments", async (DevicePostureEvidence request, IdentityDbContext db, IConfiguration configuration, DevicePosturePolicyEvaluator evaluator, HttpContext http, CancellationToken ct) =>
        {
            var policy = await GetPolicyAsync(db, configuration, ct);
            var normalized = DevicePostureEvidenceNormalizer.Normalize(request);
            if (!JsonSerializer.Deserialize<string[]>(policy.ProvidersJson)!.Contains(normalized.Provider, StringComparer.OrdinalIgnoreCase)) return Results.BadRequest(new { error = "provider_not_enabled" });
            var existing = await db.DevicePostureAssessments.AnyAsync(item => item.Provider == normalized.Provider && item.EvidenceHash == normalized.Hash, ct);
            if (existing) return Results.Conflict(new { error = "replayed_evidence" });
            var evaluation = evaluator.Evaluate(policy, request, DateTime.UtcNow);
            var assessment = new DevicePostureAssessment
            {
                UserId = request.UserId, DeviceId = normalized.DeviceId, Provider = normalized.Provider,
                EvidenceHash = normalized.Hash, SignalsJson = JsonSerializer.Serialize(normalized.Signals),
                ObservedAt = request.ObservedAt.ToUniversalTime(), ExpiresAt = evaluation.ExpiresAt,
                PolicyVersion = policy.Version, Decision = evaluation.Decision,
                CorrelationId = http.TraceIdentifier
            };
            db.DevicePostureAssessments.Add(assessment);
            WriteAudit(db, http, "CREATE", "DevicePostureAssessment", assessment.Id.ToString(), null, JsonSerializer.Serialize(new { assessment.UserId, assessment.DeviceId, assessment.Provider, assessment.Decision, assessment.PolicyVersion }));
            await db.SaveChangesAsync(ct);
            return Results.Accepted($"{IdentityApiRoutes.AdminDevicePostureAssessments}/{assessment.Id}", new { assessment.Id, assessment.Decision, evaluation.Fresh, evaluation.MeetsRequirements, assessment.ExpiresAt });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite);

        admin.MapPost("/preview", async (DevicePostureEvidence request, DevicePosturePolicyEvaluator evaluator, IdentityDbContext db, IConfiguration configuration, CancellationToken ct) =>
        {
            var policy = await GetPolicyAsync(db, configuration, ct);
            var evaluation = evaluator.Evaluate(policy, request, DateTime.UtcNow);
            return Results.Ok(evaluation);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsRead);

        admin.MapGet("/assessments", async (IdentityDbContext db, FacilityContext facilityContext, CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var query = db.DevicePostureAssessments.AsNoTracking();
            var allowedFacilities = GetAllowedFacilities(facilityContext);
            if (!facilityContext.IsCrossFacility && allowedFacilities.Length > 0)
            {
                query = query.Where(assessment => db.UserFacilities.Any(membership =>
                    membership.UserId == assessment.UserId && membership.IsActive && membership.RevokedAt == null &&
                    allowedFacilities.Contains(membership.FacilityId)));
            }

            var assessments = await query
                .OrderByDescending(item => item.ObservedAt)
                .Take(100)
                .ToListAsync(ct);
            return Results.Ok(assessments.Select(item => ToAssessmentResponse(item, now)));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsRead);

        app.MapGet(IdentityApiRoutes.DevicePostureDecision + "/{userId:guid}/{deviceId}", async (Guid userId, string deviceId, IdentityDbContext db, CancellationToken ct) =>
        {
            var assessment = await db.DevicePostureAssessments.AsNoTracking().Where(item => item.UserId == userId && item.DeviceId == deviceId).OrderByDescending(item => item.ObservedAt).FirstOrDefaultAsync(ct);
            if (assessment is null) return Results.NotFound();
            return Results.Ok(new { assessment.UserId, assessment.DeviceId, assessment.Provider, assessment.Decision, fresh = assessment.ExpiresAt > DateTime.UtcNow, assessment.ExpiresAt, assessment.PolicyVersion });
        }).RequireAuthorization();
    }

    private static async Task<DevicePosturePolicy> GetPolicyAsync(IdentityDbContext db, IConfiguration configuration, CancellationToken ct)
    {
        var policy = await db.DevicePosturePolicies.SingleOrDefaultAsync(item => item.Id == "default", ct);
        if (policy is not null) return policy;
        var providers = (configuration["DEVICE_POSTURE_PROVIDERS"] ?? "chrome-enterprise,advanced-compliance,windows-local-login")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        policy = new DevicePosturePolicy
        {
            Mode = (configuration["DEVICE_POSTURE_MODE"] ?? "observe").Trim().ToLowerInvariant(),
            ProvidersJson = JsonSerializer.Serialize(providers),
            EvidenceTtlSeconds = int.TryParse(configuration["DEVICE_POSTURE_TTL_SECONDS"], out var ttl) ? Math.Clamp(ttl, 60, 3600) : 900,
            RequiredSignalsJson = JsonSerializer.Serialize((configuration["DEVICE_POSTURE_REQUIRED_SIGNALS"] ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        };
        db.DevicePosturePolicies.Add(policy);
        await db.SaveChangesAsync(ct);
        return policy;
    }

    private static object ToPolicyResponse(DevicePosturePolicy policy) => new
    {
        policy.Id, policy.Mode, providers = JsonSerializer.Deserialize<string[]>(policy.ProvidersJson) ?? [],
        policy.EvidenceTtlSeconds, requiredSignals = JsonSerializer.Deserialize<string[]>(policy.RequiredSignalsJson) ?? [], policy.Version, policy.UpdatedAt
    };

    private sealed record DevicePosturePolicySnapshot(string Mode, string[] Providers, int EvidenceTtlSeconds, string[] RequiredSignals);

    private static object ToAssessmentResponse(DevicePostureAssessment assessment, DateTime now) => new
    {
        assessment.Id,
        assessment.UserId,
        assessment.DeviceId,
        assessment.Provider,
        evidenceHashPrefix = assessment.EvidenceHash.Length > 12 ? assessment.EvidenceHash[..12] : assessment.EvidenceHash,
        assessment.ObservedAt,
        assessment.ExpiresAt,
        fresh = assessment.ExpiresAt > now,
        assessment.Decision,
        assessment.PolicyVersion,
        assessment.CorrelationId
    };

    private static void WriteAudit(IdentityDbContext db, HttpContext http, string action, string resourceType, string resourceId, string? before, string? after) => db.AuditLogs.Add(new AuditLog
    {
        UserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub") ?? "system",
        Action = action, ResourceType = resourceType, ResourceId = resourceId, BeforeJson = before, AfterJson = after,
        CorrelationId = http.TraceIdentifier, Outcome = "success", Source = "device-posture", IpAddress = http.Connection.RemoteIpAddress?.ToString(), UserAgent = http.Request.Headers.UserAgent.ToString()
    });

    private static string[] GetAllowedFacilities(FacilityContext context) => context.AuthorizedFacilities
        .Append(context.FacilityId)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

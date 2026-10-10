using System.Security.Claims;
using System.Text.Json;
using His.Hope.IdentityService.Application.DevicePosture;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Facility;
using His.Hope.Contracts.Identity;
using His.Hope.Contracts;
using His.Hope.SharedKernel.Authorization;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class DevicePostureEndpoints
{
    public static void MapDevicePostureEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup(IdentityApiRoutes.AdminDevicePosture)
            .RequireAuthorization(AuthorizationConstants.Policies.HumanAdmin);
        admin.MapGet("/policy", async (IdentityDbContext db, IConfiguration configuration, FacilityContext facilityContext, string? facilityId, CancellationToken ct) =>
        {
            var policy = await GetPolicyAsync(db, configuration, ResolveScope(facilityContext, facilityId), ct);
            return Results.Ok(ToPolicyResponse(policy));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsRead);

        admin.MapPut("/policy", async (DevicePosturePolicyInput request, IdentityDbContext db, IConfiguration configuration, FacilityContext facilityContext, HttpContext http, CancellationToken ct) =>
        {
            var mode = request.Mode.Trim().ToLowerInvariant();
            if (mode is not ("observe" or "stepup" or "deny")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["mode"] = ["Mode must be observe, stepup or deny."] });
            if (request.EvidenceTtlSeconds is < 60 or > 3600) return Results.ValidationProblem(new Dictionary<string, string[]> { ["evidenceTtlSeconds"] = ["TTL must be between 60 and 3600 seconds."] });
            var providers = request.Providers.Select(value => value.Trim().ToLowerInvariant()).Distinct().ToArray();
            if (providers.Any(provider => !DevicePostureProviders.Allowed.Contains(provider))) return Results.ValidationProblem(new Dictionary<string, string[]> { ["providers"] = ["Unknown posture provider."] });
            var signals = request.RequiredSignals.Select(value => value.Trim().ToLowerInvariant()).Where(value => value.Length > 0).Distinct().ToArray();
            if (signals.Any(value => value.Length > 64)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["requiredSignals"] = ["Signal names are limited to 64 characters."] });
            var policy = await GetPolicyAsync(db, configuration, ResolveScope(facilityContext, request.FacilityId), ct);
            var beforeJson = JsonSerializer.Serialize(ToPolicyResponse(policy));
            policy.Mode = mode;
            policy.ProvidersJson = JsonSerializer.Serialize(providers);
            policy.RequiredSignalsJson = JsonSerializer.Serialize(signals);
            policy.EvidenceTtlSeconds = request.EvidenceTtlSeconds;
            policy.Version = (int.TryParse(policy.Version, out var version) ? version + 1 : 1).ToString();
            policy.UpdatedAt = DateTime.UtcNow;
            policy.UpdatedBy = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject);
            await db.SaveChangesAsync(ct);
            WriteAudit(db, http, "UPDATE", "DevicePosturePolicy", policy.Id, beforeJson, JsonSerializer.Serialize(ToPolicyResponse(policy)));
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToPolicyResponse(policy));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsWrite);

        admin.MapPost("/policy/rollback", async (IdentityDbContext db, IConfiguration configuration, FacilityContext facilityContext, HttpContext http, string? facilityId, CancellationToken ct) =>
        {
            if (!http.User.FindAll(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.AuthenticationMethod).Any(claim => claim.Value.Equals("mfa", StringComparison.OrdinalIgnoreCase)))
                return Results.Forbid();
            var policy = await GetPolicyAsync(db, configuration, ResolveScope(facilityContext, facilityId), ct);
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
            policy.UpdatedBy = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject);
            WriteAudit(db, http, "ROLLBACK", "DevicePosturePolicy", policy.Id, beforeJson, JsonSerializer.Serialize(ToPolicyResponse(policy)));
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToPolicyResponse(policy));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsWrite);

        admin.MapPost("/assessments", async (DevicePostureEvidence request, IdentityDbContext db, IConfiguration configuration, FacilityContext facilityContext, DevicePosturePolicyEvaluator evaluator, HttpContext http, CancellationToken ct) =>
        {
            var scopeId = ResolveScope(facilityContext, request.FacilityId);
            var policy = await GetPolicyAsync(db, configuration, scopeId, ct);
            (string Provider, string DeviceId, Dictionary<string, bool> Signals, string Hash) normalized;
            try
            {
                normalized = DevicePostureEvidenceNormalizer.Normalize(request);
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(statusCode: 400, detail: ex.Message,
                    extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = "invalid_request" });
            }
            if (!(JsonSerializer.Deserialize<string[]>(policy.ProvidersJson) ?? []).Contains(normalized.Provider, StringComparer.OrdinalIgnoreCase)) return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.ProviderNotEnabled });
            var existing = await db.DevicePostureAssessments.AnyAsync(item => item.ScopeId == scopeId && item.Provider == normalized.Provider && item.EvidenceHash == normalized.Hash, ct);
            if (existing) return Results.Conflict(new { errorCode = "replayed_evidence" });
            var evaluation = evaluator.Evaluate(policy, request, DateTime.UtcNow);
            var assessment = new DevicePostureAssessment
            {
                ScopeId = scopeId ?? IdentityScope.Global,
                UserId = request.UserId,
                DeviceId = normalized.DeviceId,
                Provider = normalized.Provider,
                EvidenceHash = normalized.Hash,
                SignalsJson = JsonSerializer.Serialize(normalized.Signals),
                ObservedAt = request.ObservedAt.ToUniversalTime(),
                ExpiresAt = evaluation.ExpiresAt,
                PolicyVersion = policy.Version,
                Decision = evaluation.Decision,
                CorrelationId = http.TraceIdentifier
            };
            db.DevicePostureAssessments.Add(assessment);
            WriteAudit(db, http, "CREATE", "DevicePostureAssessment", assessment.Id.ToString(), null, JsonSerializer.Serialize(new { assessment.UserId, assessment.DeviceId, assessment.Provider, assessment.Decision, assessment.PolicyVersion }));
            await db.SaveChangesAsync(ct);
            return Results.Accepted($"{IdentityApiRoutes.AdminDevicePostureAssessments}/{assessment.Id}", new { assessment.Id, assessment.Decision, evaluation.Fresh, evaluation.MeetsRequirements, assessment.ExpiresAt });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite);

        admin.MapPost("/preview", async (DevicePostureEvidence request, DevicePosturePolicyEvaluator evaluator, IdentityDbContext db, IConfiguration configuration, FacilityContext facilityContext, CancellationToken ct) =>
        {
            var policy = await GetPolicyAsync(db, configuration, ResolveScope(facilityContext, request.FacilityId), ct);
            var evaluation = evaluator.Evaluate(policy, request, DateTime.UtcNow);
            return Results.Ok(evaluation);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsRead);

        admin.MapGet("/assessments", async (IdentityDbContext db, FacilityContext facilityContext, string? facilityId, CancellationToken ct) =>
        {
            var now = DateTime.UtcNow;
            var query = db.DevicePostureAssessments.AsNoTracking();
            var scopeId = ResolveScope(facilityContext, facilityId);
            if (scopeId is not null) query = query.Where(assessment => assessment.ScopeId == scopeId);

            var assessments = await query
                .OrderByDescending(item => item.ObservedAt)
                .Take(100)
                .ToListAsync(ct);
            return Results.Ok(assessments.Select(item => ToAssessmentResponse(item, now)));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsRead);

        app.MapGet(IdentityApiRoutes.DevicePostureDecision + "/{userId:guid}/{deviceId}", async (
            Guid userId,
            string deviceId,
            IdentityDbContext db,
            FacilityContext facilityContext,
            HttpContext http,
            IAuthorizationService authorization,
            CancellationToken ct) =>
        {
            if (!await CanReadPostureDecisionAsync(http, authorization, userId))
                return Results.Forbid();

            var scopeId = ResolveScope(facilityContext, null);
            var assessment = await db.DevicePostureAssessments.AsNoTracking()
                .Where(item => item.UserId == userId && item.DeviceId == deviceId && (scopeId == null || item.ScopeId == scopeId))
                .OrderByDescending(item => item.ObservedAt)
                .FirstOrDefaultAsync(ct);
            assessment = Guard.Against.NotFound(assessment, "DevicePostureAssessment", $"{userId}/{deviceId}");
            return Results.Ok(new { assessment.UserId, assessment.DeviceId, assessment.Provider, assessment.Decision, fresh = assessment.ExpiresAt > DateTime.UtcNow, assessment.ExpiresAt, assessment.PolicyVersion });
        }).RequireAuthorization();
    }

    private static async Task<DevicePosturePolicy> GetPolicyAsync(IdentityDbContext db, IConfiguration configuration, string? scopeId, CancellationToken ct)
    {
        var policy = await db.DevicePosturePolicies
            .Where(item => item.Id == "default" && (item.ScopeId == IdentityScope.Global || item.ScopeId == scopeId))
            .OrderByDescending(item => item.ScopeId == scopeId && scopeId != IdentityScope.Global)
            .FirstOrDefaultAsync(ct);
        if (policy is not null) return policy;
        var providers = (configuration["DEVICE_POSTURE_PROVIDERS"] ?? "chrome-enterprise,advanced-compliance,windows-local-login")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        policy = new DevicePosturePolicy
        {
            ScopeId = scopeId ?? IdentityScope.Global,
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
        policy.Id,
        policy.Mode,
        providers = JsonSerializer.Deserialize<string[]>(policy.ProvidersJson) ?? [],
        policy.EvidenceTtlSeconds,
        requiredSignals = JsonSerializer.Deserialize<string[]>(policy.RequiredSignalsJson) ?? [],
        policy.Version,
        policy.UpdatedAt
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
        UserId = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject) ?? "system",
        Action = action,
        ResourceType = resourceType,
        ResourceId = resourceId,
        BeforeJson = before,
        AfterJson = after,
        CorrelationId = http.TraceIdentifier,
        Outcome = "success",
        Source = "device-posture",
        IpAddress = http.Connection.RemoteIpAddress?.ToString(),
        UserAgent = http.Request.Headers.UserAgent.ToString()
    });

    private static string? ResolveScope(FacilityContext context, string? requestedFacility) =>
        !context.IsCrossFacility && !string.IsNullOrWhiteSpace(context.FacilityId)
            ? context.FacilityId
            : string.IsNullOrWhiteSpace(requestedFacility) ? IdentityScope.Global : requestedFacility.Trim();

    private static string[] GetAllowedFacilities(FacilityContext context) => context.AuthorizedFacilities
        .Append(context.FacilityId)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static async Task<bool> CanReadPostureDecisionAsync(
        HttpContext http,
        IAuthorizationService authorization,
        Guid userId)
    {
        var subject = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject);
        if (Guid.TryParse(subject, out var currentUserId) && currentUserId == userId)
            return true;

        var admin = await authorization.AuthorizeAsync(
            http.User,
            null,
            AuthorizationPolicyNames.Permissions.AdminSettingsRead);
        return admin.Succeeded;
    }
}

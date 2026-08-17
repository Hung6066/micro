using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Facility;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class DirectoryProvisioningEndpoints
{
    public static void MapDirectoryProvisioningEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(IdentityApiRoutes.AdminProvisioning)
            .RequireAuthorization(AuthorizationConstants.Policies.HumanAdmin)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead);
        group.MapGet("/readiness", (IConfiguration configuration) =>
        {
            var mode = (configuration["PROVISIONING_MODE"] ?? "dry-run").Trim().ToLowerInvariant();
            var rows = new[]
            {
                Readiness("scim", configuration.GetValue<bool>("Provisioning:Scim:Enabled"), configuration["PROVISIONING_SCIM_BASE_URL"], configuration["PROVISIONING_SCIM_TOKEN_URL"], configuration["PROVISIONING_SCIM_CLIENT_ID"]),
                Readiness("entra", configuration.GetValue<bool>("Provisioning:Entra:Enabled"), configuration["Provisioning:Entra:BaseUrl"], configuration["Provisioning:Entra:TokenUrl"], configuration["Provisioning:Entra:ClientId"]),
                Readiness("google-workspace", configuration.GetValue<bool>("Provisioning:GoogleWorkspace:Enabled"), configuration["Provisioning:GoogleWorkspace:BaseUrl"], configuration["Provisioning:GoogleWorkspace:TokenUrl"], configuration["Provisioning:GoogleWorkspace:ServiceAccountSecretId"])
            };
            return Results.Ok(new { mode, targets = rows });
        });
        group.MapGet("/delivery-health", async (IdentityDbContext db, IConfiguration configuration, CancellationToken ct) =>
        {
            var provisioning = await db.DirectoryProvisioningOutbox.AsNoTracking()
                .Where(item => item.CompletedAt == null)
                .GroupBy(item => item.Target)
                .Select(items => new
                {
                    channel = "provisioning",
                    target = items.Key,
                    pending = items.Count(),
                    failed = items.Count(item => item.Attempts > 0 && item.LastError != null),
                    oldestAvailableAt = items.Min(item => (DateTime?)item.AvailableAt)
                })
                .ToListAsync(ct);
            var ssfPending = await db.SecuritySignalOutbox.AsNoTracking()
                .Where(item => item.DispatchedAt == null)
                .Select(item => new { item.Attempts, item.LastError, item.AvailableAt })
                .ToListAsync(ct);
            var ssfEnabled = configuration.GetValue("SSF_ENABLED", configuration.GetValue("SecuritySignals:Enabled", false));
            var deliveries = provisioning
                .Select(item => new DeliveryHealthRow(item.channel, item.target, item.pending, item.failed, item.oldestAvailableAt))
                .Append(new DeliveryHealthRow(
                    "ssf", "security-signal-receiver", ssfPending.Count,
                    ssfPending.Count(item => item.Attempts > 0 && item.LastError != null),
                    ssfPending.Count == 0 ? null : ssfPending.Min(item => (DateTime?)item.AvailableAt)))
                .ToArray();
            return Results.Ok(new
            {
                mode = (configuration["PROVISIONING_MODE"] ?? "dry-run").Trim().ToLowerInvariant(),
                ssfEnabled,
                generatedAt = DateTime.UtcNow,
                deliveries = deliveries.Select(item => new
                {
                    item.channel,
                    item.target,
                    item.pending,
                    item.failed,
                    item.oldestAvailableAt,
                    status = item.failed > 0 ? "failed" : item.pending > 0 ? "pending" : "healthy"
                })
            });
        });
        group.MapPost("/queue", async (ProvisioningQueueRequest request, IdentityDbContext db, FacilityContext facilityContext, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Target) || string.IsNullOrWhiteSpace(request.Operation) ||
                string.IsNullOrWhiteSpace(request.ResourceType) || string.IsNullOrWhiteSpace(request.ResourceId))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Target, operation, resourceType and resourceId are required."] });
            if (request.Operation is not ("create" or "update" or "delete"))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["operation"] = ["Operation must be create, update or delete."] });
            var target = request.Target.Trim().ToLowerInvariant();
            if (target is not ("scim" or "entra" or "google-workspace"))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["target"] = ["Unknown provisioning target."] });
            if (!request.ResourceType.Equals("User", StringComparison.OrdinalIgnoreCase) &&
                !request.ResourceType.Equals("Group", StringComparison.OrdinalIgnoreCase))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["resourceType"] = ["Only User and Group resources are supported."] });

            if (request.ResourceType.Equals("User", StringComparison.OrdinalIgnoreCase) &&
                !facilityContext.IsCrossFacility && GetAllowedFacilities(facilityContext).Length > 0 &&
                Guid.TryParse(request.ResourceId, out var userId))
            {
                var allowedFacilities = GetAllowedFacilities(facilityContext);
                var allowed = await db.UserFacilities.AnyAsync(membership =>
                    membership.UserId == userId && membership.IsActive && membership.RevokedAt == null &&
                    allowedFacilities.Contains(membership.FacilityId), ct);
                if (!allowed) return Results.Forbid();
            }

            var payload = request.Payload.ValueKind == JsonValueKind.Undefined ? "{}" : request.Payload.GetRawText();
            if (payload.Length > 256 * 1024)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["payload"] = ["Payload exceeds 256 KiB."] });
            var entry = new DirectoryProvisioningOutbox
            {
                Target = target,
                Operation = request.Operation.Trim().ToLowerInvariant(),
                ResourceType = request.ResourceType.Trim(),
                ResourceId = request.ResourceId.Trim(),
                ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim(),
                PayloadJson = payload
            };
            db.DirectoryProvisioningOutbox.Add(entry);
            await db.SaveChangesAsync(ct);
            return Results.Accepted(IdentityApiRoutes.AdminProvisioningJob(entry.Id), new { entry.Id, entry.Target, entry.Operation, entry.ResourceType, entry.ResourceId });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite);
        group.MapGet("/jobs/{id:guid}", async (Guid id, IdentityDbContext db, FacilityContext facilityContext, CancellationToken ct) =>
        {
            var entry = await db.DirectoryProvisioningOutbox.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
            return entry is null ? Results.NotFound() : await HasFacilityAccessAsync(db, facilityContext, entry, ct) ? Results.Ok(ToResponse(entry)) : Results.Forbid();
        });
        group.MapGet("/jobs", async (IdentityDbContext db, FacilityContext facilityContext, CancellationToken ct) =>
        {
            var jobs = await db.DirectoryProvisioningOutbox.AsNoTracking()
                .OrderByDescending(item => item.CreatedAt)
                .Take(100)
                .ToListAsync(ct);
            if (!facilityContext.IsCrossFacility && GetAllowedFacilities(facilityContext).Length > 0)
            {
                var visible = new List<DirectoryProvisioningOutbox>(jobs.Count);
                foreach (var job in jobs)
                    if (await HasFacilityAccessAsync(db, facilityContext, job, ct)) visible.Add(job);
                jobs = visible;
            }
            return Results.Ok(jobs.Select(ToResponse));
        });
        group.MapPost("/jobs/{id:guid}/retry", async (Guid id, IdentityDbContext db, FacilityContext facilityContext, CancellationToken ct) =>
        {
            var entry = await db.DirectoryProvisioningOutbox.SingleOrDefaultAsync(item => item.Id == id, ct);
            if (entry is null) return Results.NotFound();
            if (!await HasFacilityAccessAsync(db, facilityContext, entry, ct)) return Results.Forbid();
            entry.CompletedAt = null;
            entry.AvailableAt = DateTime.UtcNow;
            entry.LastError = null;
            await db.SaveChangesAsync(ct);
            return Results.Accepted(IdentityApiRoutes.AdminProvisioningJob(id), new { entry.Id, status = "queued" });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite);
        group.MapPost("/reconcile/{target}", async (string target, UserManager<User> users, IdentityDbContext db, FacilityContext facilityContext, CancellationToken ct) =>
        {
            target = target.Trim().ToLowerInvariant();
            if (target is not ("scim" or "entra" or "google-workspace"))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["target"] = ["Unknown provisioning target."] });
            var sourceQuery = users.Users.AsNoTracking().Where(user => user.IsActive);
            if (!facilityContext.IsCrossFacility && GetAllowedFacilities(facilityContext).Length > 0)
            {
                var facilities = GetAllowedFacilities(facilityContext);
                sourceQuery = sourceQuery.Where(user => user.FacilityMemberships.Any(membership =>
                    membership.IsActive && membership.RevokedAt == null && facilities.Contains(membership.FacilityId)));
            }
            var sourceUsers = await sourceQuery.ToListAsync(ct);
            if (sourceUsers.Count > 10_000)
                return Results.Problem("Reconciliation exceeds the single-run safety limit; use a paged job.", statusCode: StatusCodes.Status413PayloadTooLarge);
            var existing = (await db.DirectoryProvisioningOutbox
                .Where(item => item.Target == target && item.CompletedAt == null)
                .Select(item => item.ResourceId).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var bound = await db.DirectoryProvisioningBindings
                .Where(item => item.Target == target && item.ResourceType == "User")
                .ToDictionaryAsync(item => item.ResourceId, item => item.ExternalId, StringComparer.OrdinalIgnoreCase, ct);
            var queued = 0;
            foreach (var user in sourceUsers)
            {
                var resourceId = user.Id.ToString();
                if (!existing.Add(resourceId)) continue;
                db.DirectoryProvisioningOutbox.Add(new DirectoryProvisioningOutbox
                {
                    Target = target,
                    Operation = bound.ContainsKey(resourceId) ? "update" : "create",
                    ResourceType = "User",
                    ResourceId = resourceId,
                    ExternalId = bound.GetValueOrDefault(resourceId),
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        userName = user.UserName,
                        primaryEmail = user.Email,
                        name = new { givenName = user.FirstName, familyName = user.LastName },
                        active = user.IsActive
                    })
                });
                queued++;
            }
            await db.SaveChangesAsync(ct);
            return Results.Accepted(value: new { target, queued, sourceCount = sourceUsers.Count });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite);
    }

    public sealed record ProvisioningQueueRequest(
        string Target,
        string Operation,
        string ResourceType,
        string ResourceId,
        JsonElement Payload,
        string? ExternalId = null);

    private sealed record DeliveryHealthRow(string channel, string target, int pending, int failed, DateTime? oldestAvailableAt);

    private static object ToResponse(DirectoryProvisioningOutbox entry) => new
    {
        entry.Id, entry.Target, entry.Operation, entry.ResourceType, entry.ResourceId,
        entry.ExternalId, entry.CompletedAt, entry.Attempts, entry.LastError,
        status = entry.LastError == "dry_run_no_external_call" ? "dry-run" :
            entry.CompletedAt is not null ? "completed" :
            entry.LastError is not null ? "failed" : "queued"
    };

    private static object Readiness(string target, bool enabled, string? baseUrl, string? tokenUrl, string? credentialReference)
    {
        var baseUriValid = Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) && baseUri.Scheme == Uri.UriSchemeHttps;
        var tokenUriValid = Uri.TryCreate(tokenUrl, UriKind.Absolute, out var tokenUri) && tokenUri.Scheme == Uri.UriSchemeHttps;
        var credentialConfigured = !string.IsNullOrWhiteSpace(credentialReference);
        var status = !enabled ? "disabled" : !baseUriValid || !tokenUriValid || !credentialConfigured ? "configuration_missing" : "ready_for_dry_run";
        return new { target, enabled, status, endpointHost = baseUriValid ? baseUri!.Host : null, tokenHost = tokenUriValid ? tokenUri!.Host : null, credentialConfigured };
    }

    private static async Task<bool> HasFacilityAccessAsync(IdentityDbContext db, FacilityContext? facilityContext, DirectoryProvisioningOutbox entry, CancellationToken ct)
    {
        if (facilityContext is null || facilityContext.IsCrossFacility ||
            !entry.ResourceType.Equals("User", StringComparison.OrdinalIgnoreCase) || !Guid.TryParse(entry.ResourceId, out var userId)) return true;
        var facilities = GetAllowedFacilities(facilityContext);
        if (facilities.Length == 0) return true;
        return await db.UserFacilities.AnyAsync(membership => membership.UserId == userId && membership.IsActive &&
            membership.RevokedAt == null && facilities.Contains(membership.FacilityId), ct);
    }

    private static string[] GetAllowedFacilities(FacilityContext context) => context.AuthorizedFacilities
        .Append(context.FacilityId)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

using System.Security.Claims;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.Infrastructure.Audit;
using His.Hope.Infrastructure.Security;
using His.Hope.Authorization;
using His.Hope.SharedKernel.Authorization;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.IdentityService.Application.Authorization;
using His.Hope.IdentityService.Api.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using His.Hope.Contracts.Identity;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// Server-side governance endpoints for the admin-app. Break-glass requests
/// are persisted and audited; they are not themselves a permission grant.
/// Approved requests are projected into newly issued permission claims. Approval
/// revokes the subject's existing tokens so the elevated grant cannot remain
/// active in an already issued token.
/// </summary>
public static class AccessGovernanceEndpoints
{
    public static RouteGroupBuilder MapAccessGovernanceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/policies", async (
            IApplicationDbContext db,
            IdentityDbContext identityDb,
            HttpContext http,
            CancellationToken ct) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);

            var policyOwners = await IamTenantQueryExtensions.ResolveTenantPolicyOwnersAsync(db, tenantFilter, ct);
            var policyQuery = db.AuthorizationPolicies.AsNoTracking();
            if (policyOwners is not null)
                policyQuery = policyQuery.Where(item => policyOwners.Contains(item.Owner));

            var policies = await policyQuery
                .OrderBy(item => item.Key).ThenByDescending(item => item.Version)
                .Take(500)
                .Select(item => new AuthorizationPolicyDto(item.Id, item.Key, item.Description, item.Owner, item.Version, item.LifecycleStatus, item.RulesJson, item.CreatedBy, item.CreatedAt, item.PublishedAt, item.PublishedBy))
                .ToListAsync(ct);
            return Results.Ok(policies);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminPolicySimulate)
            .WithTenantReadScope(HisHopePermissions.Admin.PolicySimulate);

        // Consumers receive the latest durable, deterministic signed snapshot.
        // Draft rows are never exposed as a bundle. A missing artifact is an
        // explicit release/configuration error rather than an implicit publish.
        group.MapGet("/policies/bundle", async (
            IApplicationDbContext db,
            CancellationToken ct) =>
        {
            var artifact = await db.AuthorizationPolicyBundles.AsNoTracking()
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (artifact is null)
                return Results.NotFound("published_policy_bundle_not_found");
            using var policies = JsonDocument.Parse(artifact.PoliciesJson);
            return Results.Ok(new
            {
                schemaVersion = artifact.SchemaVersion,
                hash = artifact.Hash,
                keyId = artifact.KeyId,
                signature = artifact.Signature,
                generatedAt = artifact.CreatedAt,
                policies = policies.RootElement.Clone()
            });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminPolicySimulate);

        group.MapPost("/policies/bundle/publish", async (
            IApplicationDbContext db,
            IVaultKeyProvider keyProvider,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp) return stepUp;
            var artifact = await CreatePolicyBundleArtifactAsync(db, keyProvider, Actor(http), ct);
            var existing = await db.AuthorizationPolicyBundles.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Hash == artifact.Hash, ct);
            if (existing is not null)
                return Results.Ok(new
                {
                    existing.Id,
                    schemaVersion = existing.SchemaVersion,
                    existing.Hash,
                    existing.KeyId,
                    existing.CreatedAt,
                    idempotent = true
                });
            db.AuthorizationPolicyBundles.Add(artifact);
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "POLICY_BUNDLE_PUBLISH", "AuthorizationPolicyBundle", artifact.Id.ToString(), "Published signed authorization policy bundle.", null, artifact.Hash, ct);
            return Results.Created($"/api/v1/admin/policies/bundle/{artifact.Id}", new
            {
                artifact.Id,
                schemaVersion = artifact.SchemaVersion,
                artifact.Hash,
                artifact.KeyId,
                artifact.CreatedAt
            });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsWrite);

        // Lint is deliberately read-only and available before publish so the
        // admin workbench can show deterministic policy errors without
        // mutating the draft or putting evaluator logic in the browser.
        group.MapPost("/policies/{id:guid}/lint", async (
            Guid id,
            IApplicationDbContext db,
            CancellationToken ct) =>
        {
            var policy = Guard.Against.NotFound(
                await db.AuthorizationPolicies.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == id, ct), "AuthorizationPolicy", id);
            var valid = AbacPolicyEvaluator.TryValidate(policy.RulesJson, out var errors);
            return Results.Ok(new
            {
                policy.Id,
                policy.Key,
                policy.Version,
                valid,
                errors,
                checkedAt = DateTime.UtcNow
            });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminPolicySimulate);

        // Compile a validated draft into a deterministic, hash-addressed
        // artifact. This is read-only: publish remains the separate
        // MFA/maker-checker transition and never trusts browser-side JSON.
        group.MapPost("/policies/{id:guid}/compile", async (
            Guid id,
            IApplicationDbContext db,
            CancellationToken ct) =>
        {
            var policy = Guard.Against.NotFound(
                await db.AuthorizationPolicies.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == id, ct), "AuthorizationPolicy", id);
            if (!AbacPolicyEvaluator.TryValidate(policy.RulesJson, out var errors))
                return Results.Ok(new
                {
                    schemaVersion = "authorization-policy-compile.v1",
                    policyId = policy.Id,
                    policyKey = policy.Key,
                    version = policy.Version,
                    valid = false,
                    artifact = (string?)null,
                    hash = (string?)null,
                    errors,
                    compiledAt = DateTime.UtcNow
                });

            using var document = JsonDocument.Parse(policy.RulesJson);
            var artifact = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = false });
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(artifact))).ToLowerInvariant();
            return Results.Ok(new
            {
                schemaVersion = "authorization-policy-compile.v1",
                policyId = policy.Id,
                policyKey = policy.Key,
                version = policy.Version,
                valid = true,
                artifact,
                hash,
                errors = Array.Empty<string>(),
                compiledAt = DateTime.UtcNow
            });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminPolicySimulate);

        group.MapPost(IdentityApiRoutes.AdminRebacListObjects[IdentityApiRoutes.Admin.Length..], async (
            RebacListObjectsRequest request,
            IOpenFgaClient openFga,
            CancellationToken ct) =>
        {
            if (request.SubjectId == Guid.Empty || string.IsNullOrWhiteSpace(request.Relation) || string.IsNullOrWhiteSpace(request.ObjectType))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["subjectId, relation and objectType are required."] });
            var objects = await openFga.ListObjectsAsync($"user:{request.SubjectId}", NormalizeRelation(request.Relation), request.ObjectType.Trim(), ct);
            return objects is null
                ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
                : Results.Ok(new { request.SubjectId, relation = NormalizeRelation(request.Relation), objectType = request.ObjectType.Trim(), objects });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminPolicySimulate);

        group.MapPost("/policies", async (
            AuthorizationPolicyCreateRequest request,
            IApplicationDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Key) || request.Key.Length > 128 || string.IsNullOrWhiteSpace(request.Description))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["key and description are required."] });
            if (!AbacPolicyEvaluator.TryValidate(request.RulesJson, out var errors))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["rulesJson"] = errors });
            var key = request.Key.Trim().ToLowerInvariant();
            if (await db.AuthorizationPolicies.AnyAsync(item => item.Key == key && item.LifecycleStatus != "retired", ct))
                return Results.Conflict(new { errorCode = "policy_key_exists" });
            var policy = new AuthorizationPolicyDefinition
            {
                Key = key,
                Description = request.Description.Trim(),
                Owner = string.IsNullOrWhiteSpace(request.Owner) ? "identity-service" : request.Owner.Trim(),
                RulesJson = request.RulesJson,
                LifecycleStatus = "draft",
                CreatedBy = Actor(http)
            };
            db.AuthorizationPolicies.Add(policy);
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "POLICY_CREATE", "AuthorizationPolicy", policy.Id.ToString(), "ABAC policy draft created.", null, policy.RulesJson, ct);
            return Results.Created($"/api/v1/admin/policies/{policy.Id}", ToPolicyDto(policy));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsWrite);

        group.MapPut("/policies/{id:guid}", async (
            Guid id,
            AuthorizationPolicyUpdateRequest request,
            IApplicationDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!AbacPolicyEvaluator.TryValidate(request.RulesJson, out var errors))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["rulesJson"] = errors });
            var policy = Guard.Against.NotFound(
                await db.AuthorizationPolicies.FirstOrDefaultAsync(item => item.Id == id, ct), "AuthorizationPolicy", id);
            if (policy.LifecycleStatus == "published") return Results.Conflict(new { errorCode = "published_policy_immutable" });
            var before = policy.RulesJson;
            policy.Description = request.Description.Trim();
            policy.Owner = string.IsNullOrWhiteSpace(request.Owner) ? policy.Owner : request.Owner.Trim();
            policy.RulesJson = request.RulesJson;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "POLICY_UPDATE", "AuthorizationPolicy", policy.Id.ToString(), "ABAC policy draft updated.", before, policy.RulesJson, ct);
            return Results.Ok(ToPolicyDto(policy));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsWrite);

        group.MapPost("/policies/{id:guid}/publish", async (
            Guid id,
            IApplicationDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp) return stepUp;
            var policy = Guard.Against.NotFound(
                await db.AuthorizationPolicies.FirstOrDefaultAsync(item => item.Id == id, ct), "AuthorizationPolicy", id);
            if (policy.CreatedBy is not null && string.Equals(policy.CreatedBy, Actor(http), StringComparison.Ordinal))
                return Results.Conflict(new { errorCode = "maker_checker_required" });
            if (!AbacPolicyEvaluator.TryValidate(policy.RulesJson, out var errors))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["rulesJson"] = errors });

            var changeRequestValue = http.Request.Query["changeRequestId"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(changeRequestValue) && !Guid.TryParse(changeRequestValue, out var changeRequestId))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["changeRequestId"] = ["changeRequestId must be a valid GUID."] });
            AuthorizationChangeRequest? approvedChange = null;
            if (Guid.TryParse(changeRequestValue, out changeRequestId))
            {
                approvedChange = await AuthorizationChangeRequestWorkflow.FindApprovedAsync(
                    db, changeRequestId, "AuthorizationPolicy", id, "policy.publish",
                    AuthorizationChangeRequestWorkflow.Actor(http), ct);
                if (approvedChange is null)
                    return Results.Conflict(new { errorCode = "authorization_change_not_approved" });
                using var snapshot = JsonDocument.Parse(approvedChange.PayloadJson);
                if (!snapshot.RootElement.TryGetProperty("version", out var version) || version.GetInt32() != policy.Version)
                    return Results.Conflict(new { errorCode = "authorization_change_stale" });
            }
            else
            {
                var pending = await AuthorizationChangeRequestWorkflow.CreatePendingAsync(
                    db, http, "AuthorizationPolicy", id, "policy.publish",
                    JsonSerializer.Serialize(new { version = policy.Version }),
                    "Authorization policy publish requires independent approval.", ct);
                return Results.Accepted($"/api/v1/admin/authorization-change-requests/{pending.Id:D}", new
                {
                    changeRequestId = pending.Id,
                    pending.Status,
                    pending.ExpiresAt
                });
            }

            var previous = await db.AuthorizationPolicies.Where(item => item.Key == policy.Key && item.LifecycleStatus == "published").ToListAsync(ct);
            foreach (var item in previous) item.LifecycleStatus = "retired";
            policy.LifecycleStatus = "published";
            policy.PublishedAt = DateTime.UtcNow;
            policy.PublishedBy = Actor(http);
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "POLICY_PUBLISH", "AuthorizationPolicy", policy.Id.ToString(), "ABAC policy published after MFA maker-checker.", null, policy.RulesJson, ct);
            await AuthorizationChangeRequestWorkflow.MarkExecutedAsync(db, approvedChange!, http, ct);
            return Results.Ok(ToPolicyDto(policy));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsWrite);

        group.MapPost("/policies/{id:guid}/rollback", async (
            Guid id,
            IApplicationDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp) return stepUp;
            var current = Guard.Against.NotFound(
                await db.AuthorizationPolicies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct), "AuthorizationPolicy", id);
            var previous = await db.AuthorizationPolicies.AsNoTracking()
                .Where(item => item.Key == current.Key && item.LifecycleStatus == "retired" && item.Version < current.Version)
                .OrderByDescending(item => item.Version).FirstOrDefaultAsync(ct);
            if (previous is null) return Results.Conflict(new { errorCode = "no_previous_policy" });

            var changeRequestValue = http.Request.Query["changeRequestId"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(changeRequestValue) && !Guid.TryParse(changeRequestValue, out var changeRequestId))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["changeRequestId"] = ["changeRequestId must be a valid GUID."] });
            AuthorizationChangeRequest? approvedChange = null;
            if (Guid.TryParse(changeRequestValue, out changeRequestId))
            {
                approvedChange = await AuthorizationChangeRequestWorkflow.FindApprovedAsync(
                    db, changeRequestId, "AuthorizationPolicy", id, "policy.rollback",
                    AuthorizationChangeRequestWorkflow.Actor(http), ct);
                if (approvedChange is null)
                    return Results.Conflict(new { errorCode = "authorization_change_not_approved" });
                using var snapshot = JsonDocument.Parse(approvedChange.PayloadJson);
                if (!snapshot.RootElement.TryGetProperty("currentVersion", out var currentVersion) ||
                    !snapshot.RootElement.TryGetProperty("targetVersion", out var targetVersion) ||
                    currentVersion.GetInt32() != current.Version || targetVersion.GetInt32() != previous.Version)
                    return Results.Conflict(new { errorCode = "authorization_change_stale" });
            }
            else
            {
                var pending = await AuthorizationChangeRequestWorkflow.CreatePendingAsync(
                    db, http, "AuthorizationPolicy", id, "policy.rollback",
                    JsonSerializer.Serialize(new { currentVersion = current.Version, targetVersion = previous.Version }),
                    $"Authorization policy rollback to version {previous.Version} requires independent approval.", ct);
                return Results.Accepted($"/api/v1/admin/authorization-change-requests/{pending.Id:D}", new
                {
                    changeRequestId = pending.Id,
                    pending.Status,
                    pending.ExpiresAt,
                    targetVersion = previous.Version
                });
            }

            var published = await db.AuthorizationPolicies.Where(item => item.Key == current.Key && item.LifecycleStatus == "published").ToListAsync(ct);
            foreach (var item in published) item.LifecycleStatus = "retired";
            var rollback = new AuthorizationPolicyDefinition
            {
                Key = current.Key,
                Description = previous.Description,
                Owner = previous.Owner,
                Version = current.Version + 1,
                LifecycleStatus = "published",
                RulesJson = previous.RulesJson,
                CreatedBy = Actor(http),
                PublishedBy = Actor(http),
                PublishedAt = DateTime.UtcNow
            };
            db.AuthorizationPolicies.Add(rollback);
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "POLICY_ROLLBACK", "AuthorizationPolicy", rollback.Id.ToString(), $"ABAC policy rolled back to version {previous.Version}.", current.RulesJson, rollback.RulesJson, ct);
            await AuthorizationChangeRequestWorkflow.MarkExecutedAsync(db, approvedChange!, http, ct);
            return Results.Ok(ToPolicyDto(rollback));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsWrite);

        group.MapGet("/authorization-changes", async (
            IApplicationDbContext db,
            IdentityDbContext identityDb,
            HttpContext http,
            CancellationToken ct) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);

            var changeQuery = db.AuditLogs.AsNoTracking()
                .Where(item => item.Source == "authorization-control-plane");
            changeQuery = changeQuery.WhereTenantActor(db, tenantFilter.AllowedTenantKeys);

            var changes = await changeQuery
                .OrderByDescending(item => item.Timestamp)
                .Take(200)
                .Select(item => new
                {
                    id = item.Id,
                    item.Action,
                    item.ResourceType,
                    item.ResourceId,
                    item.UserId,
                    item.Details,
                    item.BeforeJson,
                    item.AfterJson,
                    item.Timestamp,
                    item.CorrelationId
                })
                .ToListAsync(ct);
            return Results.Ok(changes);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminAuditRead)
            .WithTenantReadScope(HisHopePermissions.Admin.AuditRead);

        group.MapGet("/access-requests", async (
            IdentityDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var query = db.AccessRequests.AsNoTracking();
            if (filter.AllowedTenantKeys is { Count: > 0 } allowedTenantKeys)
            {
                query = query.Where(item => db.UserClaims.Any(claim =>
                    claim.UserId == item.SubjectUserId &&
                    claim.ClaimType == IamTenantScopeResolver.TenantMembershipClaimType &&
                    claim.ClaimValue != null && allowedTenantKeys.Contains(claim.ClaimValue)));
            }

            var requests = await query
                .OrderByDescending(item => item.RequestedAt)
                .Take(200)
                .Select(item => new AccessRequestDto(item.Id, item.SubjectUserId, item.RequestedBy,
                    item.RoleIdsJson, item.Reason, item.Status, item.ApprovedBy,
                    item.RequestedAt, item.DecidedAt, item.ExpiresAt))
                .ToListAsync(ct);
            return Results.Ok(requests);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesRead)
            .WithTenantReadScope(HisHopePermissions.Admin.RolesRead);

        group.MapPost("/access-requests", async (
            AccessRequestCreateRequest request,
            HttpContext http,
            IApplicationDbContext db,
            IdentityDbContext identityDb,
            RoleManager<Role> roleManager,
            CancellationToken ct) =>
        {
            if (request.SubjectUserId == Guid.Empty || request.RoleIds is not { Length: > 0 } || request.Reason.Trim().Length < 10)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["subjectUserId, at least one role and a reason of at least 10 characters are required."] });

            var filter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(identityDb, request.SubjectUserId, filter, ct) is { } accessError)
                return accessError;

            var roleNames = new List<string>();
            foreach (var roleId in request.RoleIds)
            {
                var role = await roleManager.FindByIdAsync(roleId);
                if (role is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["roleIds"] = [$"Role '{roleId}' was not found."] });
                roleNames.Add(role.Name!);
            }
            if (RoleSeparationOfDuties.TryFindConflict(roleNames, out var conflict))
                return Results.Conflict(new { errorCode = "role_sod_conflict", conflict });

            var requestedBy = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject) ?? "unknown";
            var item = new AccessRequest
            {
                SubjectUserId = request.SubjectUserId,
                RequestedBy = requestedBy,
                RoleIdsJson = JsonSerializer.Serialize(request.RoleIds),
                Reason = request.Reason.Trim(),
                RequestedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(Math.Clamp(request.ExpiryHours, 1, 72)),
                Status = "pending"
            };
            db.AccessRequests.Add(item);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/admin/access-requests/{item.Id}", new { item.Id, item.Status, item.ExpiresAt });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapGet("/access-reviews", async (
            IdentityDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var query = db.AccessReviews.AsNoTracking();
            if (filter.AllowedTenantKeys is { Count: > 0 } allowedTenantKeys)
            {
                query = query.Where(item => db.UserClaims.Any(claim =>
                    claim.UserId == item.SubjectUserId &&
                    claim.ClaimType == IamTenantScopeResolver.TenantMembershipClaimType &&
                    claim.ClaimValue != null && allowedTenantKeys.Contains(claim.ClaimValue)));
            }

            var now = DateTime.UtcNow;
            var reviews = await query
                .OrderBy(item => item.DueAt).Take(200)
                .Select(item => new
                {
                    item.Id,
                    item.SubjectUserId,
                    item.Reviewer,
                    item.RoleIdsJson,
                    Status = item.Status == "pending" && item.DueAt <= now ? "overdue" : item.Status,
                    item.DecisionReason,
                    item.CreatedAt,
                    item.DueAt,
                    item.DecidedAt
                })
                .ToListAsync(ct);
            return Results.Ok(reviews.Select(item => new AccessReviewDto(item.Id, item.SubjectUserId, item.Reviewer, item.RoleIdsJson,
                item.Status, item.DecisionReason, item.CreatedAt, item.DueAt, item.DecidedAt)));
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminAuditRead)
            .WithTenantReadScope(HisHopePermissions.Admin.AuditRead);

        group.MapPost("/access-reviews", async (
            AccessReviewCreateRequest request,
            HttpContext http,
            IApplicationDbContext db,
            IdentityDbContext identityDb,
            CancellationToken ct) =>
        {
            if (request.SubjectUserId == Guid.Empty || request.RoleIds is not { Length: > 0 })
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["subjectUserId and roleIds are required."] });
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(identityDb, request.SubjectUserId, filter, ct) is { } accessError)
                return accessError;
            var reviewer = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject) ?? "unknown";
            var review = new AccessReview
            {
                SubjectUserId = request.SubjectUserId,
                Reviewer = reviewer,
                RoleIdsJson = JsonSerializer.Serialize(request.RoleIds),
                DueAt = DateTime.UtcNow.AddDays(Math.Clamp(request.DueDays, 1, 90))
            };
            db.AccessReviews.Add(review);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/admin/access-reviews/{review.Id}", new { review.Id, review.Status, review.DueAt });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/access-reviews/{id:guid}/certify", async (
            Guid id, HttpContext http, IApplicationDbContext db,
            IdentityDbContext identityDb, IAuditService audit, CancellationToken ct) =>
        {
            if (!http.User.FindAll(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.AuthenticationMethod).Any(claim => claim.Value.Equals("mfa", StringComparison.OrdinalIgnoreCase)))
                return Results.Forbid();
            var review = Guard.Against.NotFound(
                await db.AccessReviews.FirstOrDefaultAsync(item => item.Id == id, ct), "AccessReview", id);
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(identityDb, review.SubjectUserId, filter, ct) is { } accessError)
                return accessError;
            var reviewer = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject) ?? "unknown";
            if (review.Reviewer == reviewer || review.Status != "pending") return Results.Conflict(new { errorCode = "review_not_actionable" });
            if (review.DueAt <= DateTime.UtcNow)
            {
                review.Status = "overdue";
                review.DecisionReason = "Access review exceeded its due date and requires re-issuance.";
                review.DecidedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                await AdminAudit.LogAuthorizationChangeAsync(db, http, "REVIEW_OVERDUE", "AccessReview", id.ToString(), review.DecisionReason, review.RoleIdsJson, null, ct);
                await AdminAudit.LogAsync(audit, http, "REVIEW_OVERDUE", "AccessReview", id.ToString(), ct);
                return Results.Conflict(new { errorCode = "review_overdue" });
            }
            review.Status = "certified";
            review.DecisionReason = "Access retained after reviewer certification.";
            review.DecidedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "REVIEW_CERTIFY", "AccessReview", id.ToString(), review.DecisionReason, null, review.RoleIdsJson, ct);
            await AdminAudit.LogAsync(audit, http, "REVIEW_CERTIFY", "AccessReview", id.ToString(), ct);
            return Results.Ok(new { review.Id, review.Status, review.DecidedAt });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/access-reviews/{id:guid}/revoke", async (
            Guid id, HttpContext http, IApplicationDbContext db,
            IdentityDbContext identityDb, UserManager<User> userManager,
            RoleManager<Role> roleManager, ITokenBlacklistService tokenBlacklist, IAuditService audit, CancellationToken ct) =>
        {
            if (!http.User.FindAll(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.AuthenticationMethod).Any(claim => claim.Value.Equals("mfa", StringComparison.OrdinalIgnoreCase)))
                return Results.Forbid();
            var review = Guard.Against.NotFound(
                await db.AccessReviews.FirstOrDefaultAsync(item => item.Id == id, ct), "AccessReview", id);
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(identityDb, review.SubjectUserId, filter, ct) is { } accessError)
                return accessError;
            if (review.Status != "pending") return Results.Conflict(new { errorCode = "review_not_actionable" });
            if (review.DueAt <= DateTime.UtcNow)
            {
                review.Status = "overdue";
                review.DecisionReason = "Access review exceeded its due date and requires re-issuance.";
                review.DecidedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                await AdminAudit.LogAuthorizationChangeAsync(db, http, "REVIEW_OVERDUE", "AccessReview", id.ToString(), review.DecisionReason, review.RoleIdsJson, null, ct);
                await AdminAudit.LogAsync(audit, http, "REVIEW_OVERDUE", "AccessReview", id.ToString(), ct);
                return Results.Conflict(new { errorCode = "review_overdue" });
            }
            var subject = await userManager.FindByIdAsync(review.SubjectUserId.ToString());
            subject = Guard.Against.NotFound(subject, "User", review.SubjectUserId);
            var roleIds = JsonSerializer.Deserialize<string[]>(review.RoleIdsJson) ?? [];
            var roleNames = new List<string>();
            foreach (var roleId in roleIds)
            {
                var role = await roleManager.FindByIdAsync(roleId);
                if (role is not null) roleNames.Add(role.Name!);
            }
            if (roleNames.Count > 0) await userManager.RemoveFromRolesAsync(subject, roleNames);
            review.Status = "revoked";
            review.DecisionReason = "Access revoked during periodic review.";
            review.DecidedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tokenBlacklist.RevokeAllUserTokensAsync(review.SubjectUserId.ToString(), ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "REVIEW_REVOKE", "AccessReview", id.ToString(), review.DecisionReason, review.RoleIdsJson, null, ct);
            await AdminAudit.LogAsync(audit, http, "REVIEW_REVOKE", "AccessReview", id.ToString(), ct);
            return Results.Ok(new { review.Id, review.Status, review.DecidedAt });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/access-requests/{id:guid}/approve", async (
            Guid id,
            HttpContext http,
            IApplicationDbContext db,
            IdentityDbContext identityDb,
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            ITokenBlacklistService tokenBlacklist,
            IAuditService audit,
            CancellationToken ct) =>
        {
            if (!http.User.FindAll(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.AuthenticationMethod).Any(claim => claim.Value.Equals("mfa", StringComparison.OrdinalIgnoreCase)))
                return Results.Forbid();
            var item = Guard.Against.NotFound(
                await db.AccessRequests.FirstOrDefaultAsync(request => request.Id == id, ct), "AccessRequest", id);
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(identityDb, item.SubjectUserId, filter, ct) is { } accessError)
                return accessError;
            var approver = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject) ?? "unknown";
            if (string.Equals(item.RequestedBy, approver, StringComparison.OrdinalIgnoreCase))
                return Results.Conflict(new { errorCode = "maker_checker_conflict" });
            if (item.Status != "pending" || item.ExpiresAt <= DateTime.UtcNow)
                return Results.Conflict(new { errorCode = "request_not_pending" });

            var roleIds = JsonSerializer.Deserialize<string[]>(item.RoleIdsJson) ?? [];
            var governanceError = await RoleGovernanceEvaluator.ValidateRoleAssignmentAsync(
                db, http.User, item.SubjectUserId, roleIds, ct);
            if (governanceError is not null)
                return Results.Problem(governanceError, statusCode: governanceError.StartsWith("FACILITY_SCOPE_DENIED", StringComparison.Ordinal) ? 403 : 400);
            var roleNames = new List<string>();
            foreach (var roleId in roleIds)
            {
                var role = await roleManager.FindByIdAsync(roleId);
                if (role is null) return Results.Conflict(new { errorCode = "role_missing", roleId });
                roleNames.Add(role.Name!);
            }
            if (RoleSeparationOfDuties.TryFindConflict(roleNames, out var conflict))
                return Results.Conflict(new { errorCode = "role_sod_conflict", conflict });
            var user = await userManager.FindByIdAsync(item.SubjectUserId.ToString());
            user = Guard.Against.NotFound(user, "User", item.SubjectUserId);
            var currentRoles = await userManager.GetRolesAsync(user);
            if (currentRoles.Count > 0) await userManager.RemoveFromRolesAsync(user, currentRoles);
            var result = await userManager.AddToRolesAsync(user, roleNames);
            if (!result.Succeeded) return Results.Problem(string.Join(", ", result.Errors.Select(error => error.Description)), statusCode: 400);
            item.Status = "approved";
            item.ApprovedBy = approver;
            item.DecidedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tokenBlacklist.RevokeAllUserTokensAsync(item.SubjectUserId.ToString(), ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "ACCESS_APPROVE", "AccessRequest", id.ToString(), item.Reason, null, item.RoleIdsJson, ct);
            await AdminAudit.LogAsync(audit, http, "ACCESS_APPROVE", "AccessRequest", id.ToString(), ct);
            return Results.Ok(new { item.Id, item.Status, item.ApprovedBy, item.DecidedAt });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapPost("/access-requests/{id:guid}/reject", async (
            Guid id,
            HttpContext http,
            IApplicationDbContext db,
            IdentityDbContext identityDb,
            IAuditService audit,
            CancellationToken ct) =>
        {
            var item = Guard.Against.NotFound(
                await db.AccessRequests.FirstOrDefaultAsync(request => request.Id == id, ct), "AccessRequest", id);
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(identityDb, item.SubjectUserId, filter, ct) is { } accessError)
                return accessError;
            if (item.Status != "pending") return Results.Conflict(new { errorCode = "request_not_pending" });
            item.Status = "rejected";
            item.ApprovedBy = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject) ?? "unknown";
            item.DecidedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "ACCESS_REJECT", "AccessRequest", id.ToString(), item.Reason, item.RoleIdsJson, null, ct);
            await AdminAudit.LogAsync(audit, http, "ACCESS_REJECT", "AccessRequest", id.ToString(), ct);
            return Results.Ok(new { item.Id, item.Status, item.ApprovedBy, item.DecidedAt });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        group.MapGet("/users/{id:guid}/effective-access", async (
            Guid id,
            IApplicationDbContext db,
            IdentityDbContext identityDb,
            HttpContext http,
            CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(identityDb, id, filter, ct) is { } accessError)
                return accessError;

            var userEntity = Guard.Against.NotFound(
                await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct), "User", id);
            var roleIds = await db.UserRoles.AsNoTracking().Where(link => link.UserId == id).Select(link => link.RoleId).ToArrayAsync(ct);
            var roles = await db.Roles.AsNoTracking().Where(role => roleIds.Contains(role.Id)).Select(role => role.Name!).ToArrayAsync(ct);
            var permissions = await db.RolePermissions.AsNoTracking().Where(link => roleIds.Contains(link.RoleId)).Select(link => link.PermissionCode).Distinct().OrderBy(code => code).ToArrayAsync(ct);
            var facilities = await db.UserFacilities.AsNoTracking().Where(item => item.UserId == id && item.IsActive).Select(item => item.FacilityId).OrderBy(facility => facility).ToArrayAsync(ct);
            return Results.Ok(new { userId = id, isActive = userEntity.IsActive, roles, permissions, facilityIds = facilities, evaluatedAt = DateTime.UtcNow });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead)
            .WithTenantReadScope(HisHopePermissions.Admin.UsersRead);

        group.MapGet("/break-glass/requests", async (
            IdentityDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var filter = IamTenantHttpContext.RequireFilter(http);

            var query = db.BreakGlassRequests.AsNoTracking();
            if (filter.AllowedTenantKeys is { Count: > 0 } allowedTenantKeys)
            {
                query = query.Where(item => db.UserClaims.Any(claim =>
                    claim.UserId == item.SubjectUserId &&
                    claim.ClaimType == IamTenantScopeResolver.TenantMembershipClaimType &&
                    claim.ClaimValue != null && allowedTenantKeys.Contains(claim.ClaimValue)));
            }

            var requests = await query
                .OrderByDescending(item => item.RequestedAt)
                .Take(200)
                .Select(item => new BreakGlassRequestDto(
                    item.Id, item.SubjectUserId, item.PermissionCode, item.ResourceType,
                    item.ResourceId, item.FacilityId, item.Reason, item.Status,
                    item.RequestedBy, item.ApprovedBy, item.RequestedAt,
                    item.ApprovedAt, item.ExpiresAt, item.RevokedAt))
                .ToListAsync(ct);
            return Results.Ok(requests);
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminBreakGlassRead)
            .WithTenantReadScope(HisHopePermissions.Admin.BreakGlassRead);

        group.MapPost("/break-glass/requests", async (
            BreakGlassCreateRequest request,
            HttpContext http,
            IApplicationDbContext db,
            IdentityDbContext identityDb,
            IAuditService audit,
            CancellationToken ct) =>
        {
            if (request.SubjectUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.FacilityId) ||
                string.IsNullOrWhiteSpace(request.PermissionCode) || request.Reason.Trim().Length < 10)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["request"] = ["subjectUserId, facilityId, permissionCode and a reason of at least 10 characters are required."]
                });

            var filter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(identityDb, request.SubjectUserId, filter, ct) is { } accessError)
                return accessError;

            var registeredPrefixes = await db.IamServiceDefinitions.AsNoTracking()
                .Select(service => service.PermissionPrefix)
                .ToArrayAsync(ct);
            if (!PermissionCatalogRules.IsValid(request.PermissionCode, registeredPrefixes))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["permissionCode"] = ["Permission is not registered in the canonical permission catalog."]
                });

            var now = DateTime.UtcNow;
            var expiresAt = now.AddMinutes(Math.Clamp(request.DurationMinutes, 1, 30));
            var requestedBy = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject) ?? "unknown";
            var item = new BreakGlassRequest
            {
                Id = Guid.NewGuid(),
                SubjectUserId = request.SubjectUserId,
                PermissionCode = request.PermissionCode.Trim(),
                ResourceType = request.ResourceType?.Trim(),
                ResourceId = request.ResourceId?.Trim(),
                FacilityId = request.FacilityId.Trim(),
                Reason = request.Reason.Trim(),
                RequestedBy = requestedBy,
                RequestedAt = now,
                ExpiresAt = expiresAt,
                Status = "pending"
            };
            db.BreakGlassRequests.Add(item);
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAsync(audit, http, "BREAK_GLASS_REQUEST", "BreakGlassRequest", item.Id.ToString(), ct);
            return Results.Created($"/api/v1/admin/break-glass/requests/{item.Id}",
                new { item.Id, item.Status, item.ExpiresAt });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminBreakGlassWrite)
            .WithTenantMutationScope();

        group.MapPost("/break-glass/requests/{id:guid}/revoke", async (
            Guid id,
            HttpContext http,
            IApplicationDbContext db,
            IdentityDbContext identityDb,
            IAuditService audit,
            ITokenBlacklistService tokenBlacklist,
            CancellationToken ct) =>
        {
            var item = Guard.Against.NotFound(
                await db.BreakGlassRequests.FirstOrDefaultAsync(request => request.Id == id, ct), "BreakGlassRequest", id);
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(identityDb, item.SubjectUserId, filter, ct) is { } accessError)
                return accessError;
            if (item.Status is "revoked" or "expired") return Results.Conflict(new { errorCode = "request_closed" });
            item.Status = "revoked";
            item.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tokenBlacklist.RevokeAllUserTokensAsync(item.SubjectUserId.ToString(), ct);
            await AdminAudit.LogAsync(audit, http, "BREAK_GLASS_REVOKE", "BreakGlassRequest", id.ToString(), ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminBreakGlassWrite)
            .WithTenantMutationScope();

        group.MapPost("/break-glass/requests/{id:guid}/approve", async (
            Guid id,
            HttpContext http,
            IApplicationDbContext db,
            IdentityDbContext identityDb,
            IAuditService audit,
            ITokenBlacklistService tokenBlacklist,
            CancellationToken ct) =>
        {
            if (!http.User.FindAll(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.AuthenticationMethod).Any(claim => claim.Value.Equals("mfa", StringComparison.OrdinalIgnoreCase)))
                return Results.Forbid();
            var item = Guard.Against.NotFound(
                await db.BreakGlassRequests.FirstOrDefaultAsync(request => request.Id == id, ct), "BreakGlassRequest", id);
            var filter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(identityDb, item.SubjectUserId, filter, ct) is { } accessError)
                return accessError;
            if (item.Status != "pending" || item.ExpiresAt <= DateTime.UtcNow)
                return Results.Conflict(new { errorCode = "request_not_pending" });
            item.Status = "approved";
            item.ApprovedBy = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject) ?? "unknown";
            item.ApprovedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tokenBlacklist.RevokeAllUserTokensAsync(item.SubjectUserId.ToString(), ct);
            await AdminAudit.LogAsync(audit, http, "BREAK_GLASS_APPROVE", "BreakGlassRequest", id.ToString(), ct);
            return Results.Ok(new { item.Id, item.Status, item.ExpiresAt });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminBreakGlassWrite)
            .WithTenantMutationScope();

        group.MapPost("/policy/simulate", async (
            PolicySimulationRequest request,
            IApplicationDbContext db,
            CancellationToken ct) =>
        {
            if (request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.PermissionCode))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["request"] = ["userId and permissionCode are required."]
                });

            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.UserId, ct);
            user = Guard.Against.NotFound(user, "User", request.UserId);
            var roles = await db.UserRoles.AsNoTracking().Where(link => link.UserId == request.UserId).Select(link => link.RoleId).ToArrayAsync(ct);
            var hasRolePermission = await db.RolePermissions.AsNoTracking().AnyAsync(link => roles.Contains(link.RoleId) && link.PermissionCode == request.PermissionCode, ct);
            var hasBreakGlassPermission = await db.BreakGlassRequests.AsNoTracking().AnyAsync(item =>
                item.SubjectUserId == request.UserId && item.PermissionCode == request.PermissionCode &&
                item.Status == "approved" && item.RevokedAt == null && item.ExpiresAt > DateTime.UtcNow &&
                (string.IsNullOrWhiteSpace(request.FacilityId) || item.FacilityId == request.FacilityId), ct);
            var hasPermission = hasRolePermission || hasBreakGlassPermission;
            var facilityAllowed = string.IsNullOrWhiteSpace(request.FacilityId) || await db.UserFacilities.AsNoTracking().AnyAsync(item => item.UserId == request.UserId && item.FacilityId == request.FacilityId && item.IsActive, ct);
            var abacDecision = new AbacPolicyDecision(true, "policy_not_requested");
            if (!string.IsNullOrWhiteSpace(request.PolicyKey))
            {
                var policy = await db.AuthorizationPolicies.AsNoTracking().Where(item => item.Key == request.PolicyKey.Trim().ToLowerInvariant() && item.LifecycleStatus == "published").OrderByDescending(item => item.Version).FirstOrDefaultAsync(ct);
                policy = Guard.Against.NotFound(policy, "AuthorizationPolicy", request.PolicyKey);
                abacDecision = AbacPolicyEvaluator.Evaluate(policy.RulesJson, new AbacPolicyContext(request.FacilityId, request.PurposeOfUse, request.DevicePostureFresh, request.IsBreakGlass, request.Assurance));
            }
            var allowed = user.IsActive && hasPermission && facilityAllowed && abacDecision.Allowed;
            return Results.Ok(new
            {
                decision = allowed ? "allow" : "deny",
                reason = !user.IsActive ? "user_inactive" : !hasPermission ? "permission_missing" : !facilityAllowed ? "facility_missing" : !abacDecision.Allowed ? abacDecision.Reason : hasBreakGlassPermission && !hasRolePermission ? "approved_break_glass" : "role_and_facility_match",
                userId = request.UserId,
                request.PermissionCode,
                request.FacilityId,
                resourceType = request.ResourceType,
                resourceId = request.ResourceId,
                evaluatedAt = DateTime.UtcNow
            });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminPolicySimulate);

        return group;
    }

    public sealed record BreakGlassCreateRequest(Guid SubjectUserId, string PermissionCode, string FacilityId, string Reason, int DurationMinutes = 15, string? ResourceType = null, string? ResourceId = null);
    public sealed record BreakGlassRequestDto(Guid Id, Guid SubjectUserId, string PermissionCode, string? ResourceType, string? ResourceId, string FacilityId, string Reason, string Status, string RequestedBy, string? ApprovedBy, DateTime RequestedAt, DateTime? ApprovedAt, DateTime ExpiresAt, DateTime? RevokedAt);
    public sealed record PolicySimulationRequest(Guid UserId, string PermissionCode, string? FacilityId = null, string? ResourceType = null, string? ResourceId = null, string? PolicyKey = null, string? PurposeOfUse = null, bool DevicePostureFresh = false, bool IsBreakGlass = false, string? Assurance = null);
    public sealed record AuthorizationPolicyCreateRequest(string Key, string Description, string RulesJson, string? Owner = null);
    public sealed record AuthorizationPolicyUpdateRequest(string Description, string RulesJson, string? Owner = null);
    public sealed record AuthorizationPolicyDto(Guid Id, string Key, string Description, string Owner, int Version, string LifecycleStatus, string RulesJson, string? CreatedBy, DateTime CreatedAt, DateTime? PublishedAt, string? PublishedBy);
    public sealed record RebacListObjectsRequest(Guid SubjectId, string Relation, string ObjectType);
    public sealed record AccessRequestCreateRequest(Guid SubjectUserId, string[] RoleIds, string Reason, int ExpiryHours = 24);
    public sealed record AccessRequestDto(Guid Id, Guid SubjectUserId, string RequestedBy, string RoleIdsJson, string Reason, string Status, string? ApprovedBy, DateTime RequestedAt, DateTime? DecidedAt, DateTime ExpiresAt);
    public sealed record AccessReviewCreateRequest(Guid SubjectUserId, string[] RoleIds, int DueDays = 30);
    public sealed record AccessReviewDto(Guid Id, Guid SubjectUserId, string Reviewer, string RoleIdsJson, string Status, string? DecisionReason, DateTime CreatedAt, DateTime DueAt, DateTime? DecidedAt);

    private static string Actor(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject) ?? "system";
    private static AuthorizationPolicyDto ToPolicyDto(AuthorizationPolicyDefinition item) => new(item.Id, item.Key, item.Description, item.Owner, item.Version, item.LifecycleStatus, item.RulesJson, item.CreatedBy, item.CreatedAt, item.PublishedAt, item.PublishedBy);
    private static async Task<AuthorizationPolicyBundleArtifact> CreatePolicyBundleArtifactAsync(IApplicationDbContext db, IVaultKeyProvider keyProvider, string actor, CancellationToken ct)
    {
        var policies = await db.AuthorizationPolicies.AsNoTracking()
            .Where(item => item.LifecycleStatus == "published")
            .OrderBy(item => item.Key).ThenBy(item => item.Version)
            .Select(item => new { item.Key, item.Description, item.Owner, item.Version, item.RulesJson })
            .ToListAsync(ct);
        var canonical = JsonSerializer.Serialize(policies, new JsonSerializerOptions { WriteIndented = false });
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var key = (await keyProvider.GetJwksAsync(ct)).FirstOrDefault();
        return new AuthorizationPolicyBundleArtifact
        {
            PoliciesJson = canonical,
            Hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            Signature = await keyProvider.SignAsync(bytes, ct),
            KeyId = key?.Kid,
            CreatedBy = actor,
            CreatedAt = DateTime.UtcNow
        };
    }
    private static string NormalizeRelation(string relation) => relation.Trim().Replace(':', '_').Replace('.', '_').Replace('/', '_');
}

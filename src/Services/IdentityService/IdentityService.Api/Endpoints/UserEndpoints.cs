using His.Hope.IdentityService.Application.DTOs;
using His.Hope.Contracts;
using His.Hope.IdentityService.Application.UseCases.Users.Commands;
using His.Hope.IdentityService.Application.UseCases.Users.Queries;
using MediatR;
using His.Hope.Contracts.Query;
using His.Hope.Infrastructure.Security;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.Infrastructure.Audit;
using System.Text.Json;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.Contracts.Identity;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.IdentityService.Application.OpenIddict;
using His.Hope.IdentityService.Domain.Entities;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// User management endpoints for the Identity Service.
/// All endpoints require authorization.
/// </summary>
public static class UserEndpoints
{
    /// <summary>Shared Signals outbox event type emitted when an account is deactivated.</summary>
    public const string UserDeactivatedSignal = "user-deactivated";

    /// <summary>Problem errorCode returned when an administrator tries to verify their own email.</summary>
    public const string SelfEmailVerificationErrorCode = "self_email_verification";

    /// <summary>Problem errorCode returned when an administrator tries to set their own password.</summary>
    public const string SelfPasswordSetErrorCode = "self_password_set";

    /// <summary>Body of PUT /users/{id}/password.</summary>
    public sealed record AdminSetPasswordRequest(string NewPassword);

    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet(IdentityApiRoutes.UsersSegment, async (
            int page = 1,
            int pageSize = 20,
            string? search = null,
            string? role = null,
            bool? isActive = null,
            string? sort = null,
            IMediator mediator = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            QueryRequest normalized;
            try
            {
                normalized = new QueryRequest(
                    page,
                    pageSize,
                    search,
                    sort,
                    Filters: new Dictionary<string, string?>
                    {
                        ["role"] = role,
                        ["isActive"] = isActive?.ToString()
                    })
                    .Normalize(
                        new HashSet<string>(["username", "email", "isactive", "createdat"], StringComparer.OrdinalIgnoreCase),
                        new HashSet<string>(["role", "isActive"], StringComparer.OrdinalIgnoreCase));
            }
            catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["query"] = [ex.Message] }); }
            if (normalized.Search?.Length > 100 || (normalized.Filters?.TryGetValue("role", out var roleFilter) == true && roleFilter?.Length > 100))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["search"] = ["Search and role filters must be 100 characters or fewer."] });

            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            var result = await mediator.Send(
                new GetUsersQuery(
                    normalized.Page,
                    normalized.PageSize,
                    normalized.Search,
                    normalized.Filters?.GetValueOrDefault("role"),
                    isActive,
                    normalized.Sort,
                    tenantFilter.AllowedTenantKeys?.ToArray()), ct);
            return Results.Ok(result);
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead)
            .WithTenantReadScope(HisHopePermissions.Admin.UsersRead);

        group.MapGet(IdentityApiRoutes.UsersSegment + "/{id:guid}", async (
            Guid id,
            IMediator mediator = null!,
            IdentityDbContext db = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            var user = Guard.Against.NotFound(
                await mediator.Send(new GetUserByIdQuery(id), ct), "User", id);

            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, id, tenantFilter, ct) is { } accessError)
                return accessError;

            return Results.Ok(user);
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersRead)
            .WithTenantReadScope(HisHopePermissions.Admin.UsersRead);

        group.MapPost(IdentityApiRoutes.UsersSegment, async (
            CreateUserRequest request,
            IMediator mediator = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            try
            {
                var command = new CreateUserCommand(
                    request.Username, request.Email, request.Password,
                    request.FirstName, request.LastName, request.MiddleName,
                    request.LicenseNumber, request.Specialty,
                    request.PhoneNumber, request.Role);

                var user = await mediator.Send(command, ct);
                await AdminAudit.LogAsync(audit, http, "CREATE", "User", user.Id.ToString(), ct);
                return Results.Created($"/api/v1/auth/users/{user.Id}", user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(statusCode: 400, detail: ex.Message,
                    extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.UserRequestRejected });
            }
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite)
            .WithTenantMutationScope();

        group.MapPut(IdentityApiRoutes.UsersSegment + "/{id:guid}", async (
            Guid id,
            UpdateUserRequest request,
            IMediator mediator = null!,
            IdentityDbContext db = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            ITokenBlacklistService tokenBlacklist = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, id, tenantFilter, ct) is { } accessError)
                return accessError;

            try
            {
                var command = new UpdateUserCommand(
                    id, request.FirstName, request.LastName, request.Email,
                    request.PhoneNumber, request.Role, request.IsActive, request.ConcurrencyToken);

                var user = await mediator.Send(command, ct);
                await tokenBlacklist.RevokeAllUserTokensAsync(id.ToString(), ct);
                await AdminAudit.LogAsync(audit, http, "UPDATE", "User", id.ToString(), ct);
                return Results.Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(statusCode: ex.Message.StartsWith("CONCURRENCY_CONFLICT:", StringComparison.Ordinal) ? 409 : 400,
                    detail: ex.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = ex.Message.StartsWith("CONCURRENCY_CONFLICT:", StringComparison.Ordinal) ? ApiErrorCodes.ConcurrencyConflict : ApiErrorCodes.UserRequestRejected });
            }
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite)
            .WithTenantMutationScope();

        group.MapPut(IdentityApiRoutes.UsersSegment + "/{id:guid}/deactivate", async (
            Guid id,
            IMediator mediator = null!,
            IdentityDbContext db = null!,
            ITokenBlacklistService tokenBlacklist = null!,
            IOpenIddictSubjectRevoker subjectRevoker = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, id, tenantFilter, ct) is { } accessError)
                return accessError;

            await mediator.Send(new DeactivateUserCommand(id), ct);
            await tokenBlacklist.RevokeAllUserTokensAsync(id.ToString(), ct);
            // SECURITY (leavers): the blacklist only covers JWT access tokens.
            // Revoke OpenIddict authorizations and refresh/reference tokens too,
            // so OIDC relying parties (e.g. OpenShip consoles) cannot refresh.
            await subjectRevoker.RevokeAllAsync(id, ct);
            db.SecuritySignalOutbox.Add(new SecuritySignalOutbox
            {
                EventType = UserDeactivatedSignal,
                Subject = id.ToString(),
                PayloadJson = JsonSerializer.Serialize(new
                {
                    eventType = UserDeactivatedSignal,
                    subject = id.ToString(),
                    severity = "info",
                    occurredAt = DateTime.UtcNow
                })
            });
            await db.SaveChangesAsync(ct);
            await AdminAudit.LogAsync(audit, http, "DEACTIVATE", "User", id.ToString(), ct);
            return Results.NoContent();
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite)
            .WithTenantMutationScope();

        group.MapPut(IdentityApiRoutes.UsersSegment + "/{id:guid}/activate", async (
            Guid id,
            IMediator mediator = null!,
            IdentityDbContext db = null!,
            ITokenBlacklistService tokenBlacklist = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, id, tenantFilter, ct) is { } accessError)
                return accessError;

            await mediator.Send(new ActivateUserCommand(id), ct);
            await tokenBlacklist.RevokeAllUserTokensAsync(id.ToString(), ct);
            await AdminAudit.LogAsync(audit, http, "ACTIVATE", "User", id.ToString(), ct);
            return Results.NoContent();
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite)
            .WithTenantMutationScope();

        // Administrator-verified email. Production requires a confirmed email before a
        // password sign-in succeeds, and accounts created by an administrator start
        // unconfirmed with no way to receive the confirmation link before they can sign
        // in. Another administrator (never the account itself) vouches for the address,
        // with a fresh MFA step-up; the action is idempotent and audited.
        group.MapPut(IdentityApiRoutes.UsersSegment + "/{id:guid}/verify-email", async (
            Guid id,
            IdentityDbContext db = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp)
                return stepUp;

            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, id, tenantFilter, ct) is { } accessError)
                return accessError;

            var caller = http.User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject);
            if (string.Equals(caller, id.ToString(), StringComparison.OrdinalIgnoreCase))
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Self email verification is not allowed",
                    detail: "Another administrator must verify this account's email address.",
                    extensions: new Dictionary<string, object?> { ["errorCode"] = SelfEmailVerificationErrorCode });

            var user = await db.Users.FirstOrDefaultAsync(item => item.Id == id, ct);
            if (user is null)
                return Results.NotFound();
            if (string.IsNullOrWhiteSpace(user.Email))
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "The account has no email address",
                    extensions: new Dictionary<string, object?> { ["errorCode"] = "email_missing" });

            var changed = !user.EmailConfirmed;
            if (changed)
            {
                user.EmailConfirmed = true;
                await db.SaveChangesAsync(ct);
                await AdminAudit.LogAsync(audit, http, "VERIFY_EMAIL", "User", id.ToString(), ct);
            }

            return Results.Ok(new { id, emailConfirmed = true, changed });
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite)
            .WithTenantMutationScope();

        // Administrator-set password. Accounts created in admin-app had no way to get a
        // usable password (the edit dialog's password field was never sent). Another
        // administrator (never the account itself: that is Change password) sets one with
        // a fresh MFA step-up. The reset path enforces the password policy and the recent
        // password history; afterwards every session and token of the user is revoked.
        group.MapPut(IdentityApiRoutes.UsersSegment + "/{id:guid}/password", async (
            Guid id,
            AdminSetPasswordRequest request,
            IdentityDbContext db = null!,
            IIdentityService identityService = null!,
            ITokenBlacklistService tokenBlacklist = null!,
            IOpenIddictSubjectRevoker subjectRevoker = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp)
                return stepUp;

            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(db, id, tenantFilter, ct) is { } accessError)
                return accessError;

            var caller = http.User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
                ?? http.User.FindFirstValue(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject);
            if (string.Equals(caller, id.ToString(), StringComparison.OrdinalIgnoreCase))
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Setting your own password is not allowed",
                    detail: "Use Change password for your own account.",
                    extensions: new Dictionary<string, object?> { ["errorCode"] = SelfPasswordSetErrorCode });

            if (string.IsNullOrWhiteSpace(request?.NewPassword))
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "A new password is required",
                    extensions: new Dictionary<string, object?> { ["errorCode"] = "password_required" });

            var email = await db.Users.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => item.Email)
                .FirstOrDefaultAsync(ct);
            if (email is null)
                return Results.NotFound();

            try
            {
                var token = await identityService.GeneratePasswordResetTokenAsync(email, ct);
                await identityService.ResetPasswordAsync(email, token, request.NewPassword, ct);
            }
            catch (InvalidOperationException ex)
            {
                var reused = ex.Message.Contains("reuse", StringComparison.OrdinalIgnoreCase);
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: reused ? "The password was used recently" : "The password does not meet the policy",
                    detail: ex.Message,
                    extensions: new Dictionary<string, object?>
                    {
                        ["errorCode"] = reused ? "password_reused" : "password_policy"
                    });
            }

            await tokenBlacklist.RevokeAllUserTokensAsync(id.ToString(), ct);
            await subjectRevoker.RevokeAllAsync(id, ct);
            await AdminAudit.LogAsync(audit, http, "SET_PASSWORD", "User", id.ToString(), ct);
            return Results.NoContent();
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminUsersWrite)
            .WithTenantMutationScope();

        group.MapPut(IdentityApiRoutes.UsersSegment + "/{id:guid}/roles", async (
            Guid id,
            AssignRolesRequest request,
            IMediator mediator = null!,
            ITokenBlacklistService tokenBlacklist = null!,
            IApplicationDbContext db = null!,
            IdentityDbContext identityDb = null!,
            IAuditService audit = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);
            if (await IamTenantAccessGuard.EnsureUserAccessAsync(identityDb, id, tenantFilter, ct) is { } accessError)
                return accessError;

            try
            {
                var governanceError = await RoleGovernanceEvaluator.ValidateRoleAssignmentAsync(
                    db, http.User, id, request.RoleIds, ct);
                if (governanceError is not null)
                    return Results.Problem(statusCode: governanceError.StartsWith("FACILITY_SCOPE_DENIED", StringComparison.Ordinal) ? 403 : 400,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = governanceError.StartsWith("FACILITY_SCOPE_DENIED", StringComparison.Ordinal) ? ApiErrorCodes.FacilityScopeDenied : ApiErrorCodes.UserRequestRejected });
                var user = await mediator.Send(
                    new AssignRolesCommand(id, request.RoleIds), ct);
                await tokenBlacklist.RevokeAllUserTokensAsync(id.ToString(), ct);
                await AdminAudit.LogAuthorizationChangeAsync(
                    db, http, "ROLE_ASSIGNMENT", "User", id.ToString(),
                    "Role assignment changed through admin control plane.",
                    null, JsonSerializer.Serialize(new { roleIds = request.RoleIds }), ct);
                await AdminAudit.LogAsync(audit, http, "ASSIGN_ROLES", "User", id.ToString(), ct);
                return Results.Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(statusCode: 400, detail: ex.Message,
                    extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.UserRequestRejected });
            }
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminRolesWrite)
            .WithTenantMutationScope();

        return group;
    }
}

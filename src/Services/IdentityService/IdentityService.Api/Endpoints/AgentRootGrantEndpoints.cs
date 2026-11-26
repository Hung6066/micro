using System.Security.Claims;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Api.Composition;
using His.Hope.IdentityService.Application.InfraAccess;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.SharedKernel.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// Time-limited root SSH for the infrastructure automation agent. A super admin requests a
/// grant, a different super admin approves it with a fresh MFA, and IdentityService has the
/// Vault SSH CA sign the agent's public key for the root-breakglass principal. A workload
/// client on the K3s manager fetches the certificate of the newest active grant.
/// </summary>
public static class AgentRootGrantEndpoints
{
    public const string ResourceType = "AgentRootGrant";
    private const int ListLimit = 100;

    /// <summary>Maps on the super-admin /api/v1/admin/iam group.</summary>
    public static void MapAgentRootGrantEndpoints(this RouteGroupBuilder iam)
    {
        var group = iam.MapGroup("/infra-access/agent-root-grants");
        group.MapGet("", List);
        group.MapPost("", Create);
        group.MapPost("/{id:guid}/approve", Approve);
        group.MapPost("/{id:guid}/revoke", Revoke);
    }

    /// <summary>Maps the workload certificate fetch, like the console-access lookup.</summary>
    public static void MapAgentRootCertificateEndpoint(this WebApplication app)
    {
        var environment = app.Services.GetRequiredService<IHostEnvironment>();
        app.MapGet(IdentityApiRoutes.OpenShipAgentRootCertificate, GetCertificate)
            .RequireAuthorization(IdentityServiceRegistrationExtensions.OpenShipAgentRootCertificatePolicy)
            .AddEndpointFilter(async (context, next) =>
            {
                if (environment.IsProduction() && !context.HttpContext.Request.IsHttps)
                    return Problem(StatusCodes.Status400BadRequest, "https_required", "The agent root certificate requires HTTPS.");
                return await next(context);
            })
            .WithOpenApi();
    }

    public sealed record CreateAgentRootGrantRequest(int DurationMinutes, string? Reason);

    /// <summary>List row. The certificate itself is never part of it.</summary>
    public sealed record AgentRootGrantDto(
        Guid Id, string AgentId, string Reason, int DurationMinutes, string Status,
        Guid RequestedBy, string RequestedByEmail, DateTime RequestedAt,
        Guid? ApprovedBy, string? ApprovedByEmail, DateTime? ApprovedAt,
        DateTime? ValidBefore, string? CertificateSerial,
        Guid? RevokedBy, DateTime? RevokedAt, string? FailureReason,
        bool Active, int RemainingMinutes, bool RequestedByMe, DateTime? PendingUntil);

    public sealed record AgentRootCertificateResponse(Guid GrantId, string Certificate, DateTime ValidBefore, string? Serial);

    internal static async Task<IResult> List(
        HttpContext http, IdentityDbContext db, IOptionsMonitor<AgentRootOptions> options,
        TimeProvider clock, CancellationToken ct)
    {
        var settings = options.CurrentValue;
        var now = clock.GetUtcNow().UtcDateTime;
        await ExpireStaleAsync(db, settings, null, now, ct);
        var me = CurrentUserId(http.User);
        var grants = await db.AgentRootGrants.AsNoTracking()
            .TagWith("Identity.InfraAccess.AgentRootGrants.List")
            .OrderByDescending(item => item.RequestedAt)
            .Take(ListLimit)
            .Select(item => new AgentRootGrant
            {
                // Explicit projection so the certificate text is never even read.
                Id = item.Id, AgentId = item.AgentId, Reason = item.Reason, DurationMinutes = item.DurationMinutes,
                Status = item.Status, RequestedBy = item.RequestedBy, RequestedByEmail = item.RequestedByEmail,
                RequestedAt = item.RequestedAt, ApprovedBy = item.ApprovedBy, ApprovedByEmail = item.ApprovedByEmail,
                ApprovedAt = item.ApprovedAt, ValidBefore = item.ValidBefore, CertificateSerial = item.CertificateSerial,
                RevokedBy = item.RevokedBy, RevokedAt = item.RevokedAt, FailureReason = item.FailureReason,
            })
            .ToListAsync(ct);
        return Results.Ok(new
        {
            enabled = settings.Enabled,
            agentId = settings.AgentId,
            principal = settings.Principal,
            minMinutes = settings.MinMinutes,
            maxMinutes = settings.MaxMinutes,
            pendingTtlMinutes = settings.PendingTtlMinutes,
            items = grants.Select(item => ToDto(item, me, now, settings)).ToArray(),
        });
    }

    internal static async Task<IResult> Create(
        CreateAgentRootGrantRequest request, HttpContext http, IdentityDbContext db,
        IOptionsMonitor<AgentRootOptions> options, TimeProvider clock, CancellationToken ct)
    {
        if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp)
            return stepUp;
        var settings = options.CurrentValue;
        if (!settings.Enabled)
            return Disabled();
        var errors = AgentRootGrantRules.ValidateRequest(request.DurationMinutes, request.Reason, settings);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);
        if (await ResolveActorAsync(http, db, ct) is not { } actor)
            return Results.Forbid();

        var now = clock.GetUtcNow().UtcDateTime;
        var grant = new AgentRootGrant
        {
            Id = Guid.NewGuid(),
            AgentId = settings.AgentId,
            Reason = request.Reason!.Trim(),
            DurationMinutes = request.DurationMinutes,
            Status = AgentRootGrant.StatusPending,
            RequestedBy = actor.Id,
            RequestedByEmail = actor.Email,
            RequestedAt = now,
        };
        db.AgentRootGrants.Add(grant);
        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAuthorizationChangeAsync(db, http, "AGENT_ROOT_GRANT_REQUEST", ResourceType, grant.Id.ToString("D"),
            grant.Reason, null, Snapshot(grant), ct);
        return Results.Created($"{IdentityApiRoutes.AdminIam}/infra-access/agent-root-grants/{grant.Id:D}",
            ToDto(grant, actor.Id, now, settings));
    }

    internal static async Task<IResult> Approve(
        Guid id, HttpContext http, IdentityDbContext db, IOptionsMonitor<AgentRootOptions> options,
        IAgentRootCertificateSigner signer, TimeProvider clock, ILoggerFactory loggers, CancellationToken ct)
    {
        if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp)
            return stepUp;
        var settings = options.CurrentValue;
        if (!settings.Enabled)
            return Disabled();
        if (await ResolveActorAsync(http, db, ct) is not { } approver)
            return Results.Forbid();
        var grant = await db.AgentRootGrants.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (grant is null)
            return Problem(StatusCodes.Status404NotFound, "agent_root_grant_not_found", "The agent root grant does not exist.");

        var now = clock.GetUtcNow().UtcDateTime;
        switch (AgentRootGrantRules.CanApprove(grant, approver.Id, now, settings))
        {
            case AgentRootGrantRules.ApprovalDecision.SelfApproval:
                // SECURITY (four-eyes): the requester can never approve their own grant.
                return Problem(StatusCodes.Status403Forbidden, "agent_root_self_approval",
                    "Another super admin must approve this agent root grant.");
            case AgentRootGrantRules.ApprovalDecision.NotPending:
                return Problem(StatusCodes.Status409Conflict, "agent_root_not_pending", "The agent root grant is no longer pending.");
            case AgentRootGrantRules.ApprovalDecision.PendingExpired:
            {
                var expiredBefore = Snapshot(grant);
                grant.Status = AgentRootGrant.StatusExpired;
                await db.SaveChangesAsync(ct);
                await AdminAudit.LogAuthorizationChangeAsync(db, http, "AGENT_ROOT_GRANT_EXPIRE", ResourceType, id.ToString("D"),
                    "Pending agent root grant expired before approval.", expiredBefore, Snapshot(grant), ct);
                return Problem(StatusCodes.Status409Conflict, "agent_root_request_expired",
                    $"The request was not approved within {settings.PendingTtlMinutes} minutes; request a new grant.");
            }
        }

        // Claim the grant atomically so two approvers racing each other cannot both sign.
        var claimed = await db.AgentRootGrants
            .Where(item => item.Id == id && item.Status == AgentRootGrant.StatusPending && item.ApprovedBy == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.ApprovedBy, approver.Id)
                .SetProperty(item => item.ApprovedByEmail, approver.Email)
                .SetProperty(item => item.ApprovedAt, now), ct);
        if (claimed != 1)
            return Problem(StatusCodes.Status409Conflict, "agent_root_not_pending", "The agent root grant is no longer pending.");
        var before = Snapshot(grant);
        grant.ApprovedBy = approver.Id;
        grant.ApprovedByEmail = approver.Email;
        grant.ApprovedAt = now;
        db.Entry(grant).Property(item => item.ApprovedBy).IsModified = false;
        db.Entry(grant).Property(item => item.ApprovedByEmail).IsModified = false;
        db.Entry(grant).Property(item => item.ApprovedAt).IsModified = false;

        var keyId = AgentRootGrantRules.BuildKeyId(grant.Id, grant.RequestedByEmail, approver.Email);
        try
        {
            var signed = await signer.SignAsync(settings.PublicKey, settings.Principal,
                TimeSpan.FromMinutes(grant.DurationMinutes), keyId, ct);
            grant.Status = AgentRootGrant.StatusIssued;
            grant.ValidBefore = clock.GetUtcNow().UtcDateTime.AddMinutes(grant.DurationMinutes);
            grant.Certificate = signed.SignedKey;
            grant.CertificateSerial = Fit(signed.SerialNumber, 128);
            await db.SaveChangesAsync(ct);
        }
        catch (AgentRootSigningException exception)
        {
            loggers.CreateLogger(typeof(AgentRootGrantEndpoints)).LogWarning(
                "Agent root grant {GrantId} could not be signed: {Reason}", grant.Id, exception.Message);
            grant.Status = AgentRootGrant.StatusFailed;
            grant.FailureReason = Fit(exception.Message, 500);
            await db.SaveChangesAsync(CancellationToken.None);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "AGENT_ROOT_GRANT_APPROVE", ResourceType, id.ToString("D"),
                "Agent root grant approved but Vault signing failed.", before, Snapshot(grant), CancellationToken.None);
            return Problem(StatusCodes.Status502BadGateway, "agent_root_sign_failed",
                "Vault could not sign the agent key. The grant is marked failed; request a new one.");
        }

        // Snapshot carries the serial only; the certificate never goes into audit.
        await AdminAudit.LogAuthorizationChangeAsync(db, http, "AGENT_ROOT_GRANT_APPROVE", ResourceType, id.ToString("D"),
            "Agent root grant approved and certificate issued.", before, Snapshot(grant), ct);
        return Results.Ok(ToDto(grant, approver.Id, clock.GetUtcNow().UtcDateTime, settings));
    }

    /// <remarks>
    /// An SSH certificate cannot be recalled from Vault once signed: revocation stops the
    /// workload endpoint from distributing it, and the fetcher on the manager deletes its
    /// local copy within a minute of receiving 404. The certificate still expires at ValidBefore.
    /// </remarks>
    internal static async Task<IResult> Revoke(
        Guid id, HttpContext http, IdentityDbContext db, IOptionsMonitor<AgentRootOptions> options,
        TimeProvider clock, CancellationToken ct)
    {
        if (await ResolveActorAsync(http, db, ct) is not { } actor)
            return Results.Forbid();
        var grant = await db.AgentRootGrants.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (grant is null)
            return Problem(StatusCodes.Status404NotFound, "agent_root_grant_not_found", "The agent root grant does not exist.");
        if (!AgentRootGrantRules.CanRevoke(grant))
            return Problem(StatusCodes.Status409Conflict, "agent_root_grant_closed", "Only pending or issued grants can be revoked.");

        var now = clock.GetUtcNow().UtcDateTime;
        var before = Snapshot(grant);
        grant.Status = AgentRootGrant.StatusRevoked;
        grant.RevokedBy = actor.Id;
        grant.RevokedAt = now;
        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAuthorizationChangeAsync(db, http, "AGENT_ROOT_GRANT_REVOKE", ResourceType, id.ToString("D"),
            "Agent root grant revoked.", before, Snapshot(grant), ct);
        return Results.Ok(ToDto(grant, actor.Id, now, options.CurrentValue));
    }

    internal static async Task<IResult> GetCertificate(
        string? agentId, HttpContext http, IdentityDbContext db, IOptionsMonitor<AgentRootOptions> options,
        TimeProvider clock, CancellationToken ct)
    {
        // A certificate is a credential: never cache it anywhere on the way.
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";

        var settings = options.CurrentValue;
        var requested = string.IsNullOrWhiteSpace(agentId) ? settings.AgentId : agentId.Trim();
        if (requested.Length > 128)
            return Problem(StatusCodes.Status400BadRequest, "invalid_agent_id", "agentId is too long.");
        if (!settings.Enabled)
            return Problem(StatusCodes.Status404NotFound, "agent_root_disabled", "Agent root access is disabled.");

        var now = clock.GetUtcNow().UtcDateTime;
        await ExpireStaleAsync(db, settings, requested, now, ct);
        var grant = await db.AgentRootGrants.AsNoTracking()
            .TagWith("Identity.InfraAccess.AgentRootCertificate")
            .Where(item => item.AgentId == requested && item.Status == AgentRootGrant.StatusIssued &&
                           item.RevokedAt == null && item.ValidBefore > now && item.Certificate != null)
            .OrderByDescending(item => item.ValidBefore)
            .Select(item => new { item.Id, item.Certificate, item.ValidBefore, item.CertificateSerial })
            .FirstOrDefaultAsync(ct);
        if (grant is null)
            return Problem(StatusCodes.Status404NotFound, "agent_root_no_active_grant", "No active agent root grant.");
        return Results.Ok(new AgentRootCertificateResponse(
            grant.Id, grant.Certificate!, DateTime.SpecifyKind(grant.ValidBefore!.Value, DateTimeKind.Utc), grant.CertificateSerial));
    }

    /// <summary>Marks issued grants past ValidBefore and pending grants past the approval window as expired.</summary>
    private static async Task ExpireStaleAsync(IdentityDbContext db, AgentRootOptions settings, string? agentId, DateTime now, CancellationToken ct)
    {
        var pendingCutoff = now.AddMinutes(-settings.PendingTtlMinutes);
        await db.AgentRootGrants
            .Where(item => agentId == null || item.AgentId == agentId)
            .Where(item => (item.Status == AgentRootGrant.StatusIssued && item.ValidBefore <= now) ||
                           (item.Status == AgentRootGrant.StatusPending && item.RequestedAt <= pendingCutoff))
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, AgentRootGrant.StatusExpired), ct);
    }

    private sealed record Actor(Guid Id, string Email);

    private static Guid? CurrentUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(HisHopeProtocolConstants.Claims.Subject);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>The signed-in admin's id and email, as stored on the user (claims are only a fallback).</summary>
    private static async Task<Actor?> ResolveActorAsync(HttpContext http, IdentityDbContext db, CancellationToken ct)
    {
        if (CurrentUserId(http.User) is not { } id) return null;
        var stored = await db.Users.AsNoTracking().Where(user => user.Id == id)
            .Select(user => new { user.Email, user.UserName }).FirstOrDefaultAsync(ct);
        var email = stored?.Email ?? stored?.UserName ?? http.User.FindFirstValue("email") ??
                    http.User.FindFirstValue(ClaimTypes.Email) ?? id.ToString("D");
        return new Actor(id, Fit(email, 256));
    }

    private static AgentRootGrantDto ToDto(AgentRootGrant item, Guid? me, DateTime now, AgentRootOptions settings) => new(
        item.Id, item.AgentId, item.Reason, item.DurationMinutes, item.Status,
        item.RequestedBy, item.RequestedByEmail, Utc(item.RequestedAt)!.Value,
        item.ApprovedBy, item.ApprovedByEmail, Utc(item.ApprovedAt),
        Utc(item.ValidBefore), item.CertificateSerial,
        item.RevokedBy, Utc(item.RevokedAt), item.FailureReason,
        AgentRootGrantRules.IsActive(item, now), AgentRootGrantRules.RemainingMinutes(item, now),
        me is { } current && current == item.RequestedBy,
        item.Status == AgentRootGrant.StatusPending ? Utc(item.RequestedAt.AddMinutes(settings.PendingTtlMinutes)) : null);

    private static DateTime? Utc(DateTime? value) => value is { } v ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : null;

    /// <summary>Audit snapshot: everything except the certificate text.</summary>
    private static string Snapshot(AgentRootGrant item) => JsonSerializer.Serialize(new
    {
        item.Id, item.AgentId, item.Reason, item.DurationMinutes, item.Status,
        item.RequestedBy, item.RequestedByEmail, item.RequestedAt,
        item.ApprovedBy, item.ApprovedByEmail, item.ApprovedAt,
        item.ValidBefore, item.CertificateSerial, item.RevokedBy, item.RevokedAt, item.FailureReason,
    });

    private static string Fit(string value, int max) => value.Length <= max ? value : value[..max];

    private static IResult Disabled() =>
        Problem(StatusCodes.Status503ServiceUnavailable, "agent_root_disabled", "Agent root access is disabled on this host.");

    private static IResult Problem(int status, string code, string detail) =>
        Results.Problem(statusCode: status, detail: detail,
            extensions: new Dictionary<string, object?> { ["errorCode"] = code });
}

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
using OpenIddict.Abstractions;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// Time-limited root SSH for the infrastructure automation agent. A super admin requests a
/// grant, a different super admin approves it with a fresh MFA, and IdentityService has the
/// Vault SSH CA sign the key of every active agent device (K3s manager, operator
/// workstations) for the root-breakglass principal. Each device fetches only its own
/// certificate with its own client_credentials client.
/// </summary>
public static class AgentRootGrantEndpoints
{
    public const string ResourceType = "AgentRootGrant";
    public const string DeviceResourceType = "AgentRootDevice";
    private const int ListLimit = 100;

    /// <summary>Maps on the super-admin /api/v1/admin/iam group.</summary>
    public static void MapAgentRootGrantEndpoints(this RouteGroupBuilder iam)
    {
        var group = iam.MapGroup("/infra-access/agent-root-grants");
        group.MapGet("", List);
        group.MapPost("", Create);
        group.MapPost("/{id:guid}/approve", Approve);
        group.MapPost("/{id:guid}/revoke", Revoke);

        var devices = iam.MapGroup("/infra-access/agent-root-devices");
        devices.MapGet("", ListDevices);
        devices.MapPost("", AddDevice);
        devices.MapPost("/{id:guid}/disable", (Guid id, HttpContext http, IdentityDbContext db, TimeProvider clock, CancellationToken ct) =>
            SetDeviceActiveAsync(id, false, http, db, clock, ct));
        devices.MapPost("/{id:guid}/enable", (Guid id, HttpContext http, IdentityDbContext db, TimeProvider clock, CancellationToken ct) =>
            SetDeviceActiveAsync(id, true, http, db, clock, ct));
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

    public sealed record CreateAgentRootGrantRequest(int DurationMinutes, string? Reason, Guid[]? DeviceIds);

    /// <summary>List row. The certificate itself is never part of it.</summary>
    public sealed record AgentRootGrantDto(
        Guid Id, string AgentId, string Reason, int DurationMinutes, string Status,
        Guid RequestedBy, string RequestedByEmail, DateTime RequestedAt,
        Guid? ApprovedBy, string? ApprovedByEmail, DateTime? ApprovedAt,
        DateTime? ValidBefore, IReadOnlyList<string> RequestedDevices, IReadOnlyList<AgentRootIssuedDto> Certificates,
        Guid? RevokedBy, DateTime? RevokedAt, string? FailureReason,
        bool Active, int RemainingMinutes, bool RequestedByMe, DateTime? PendingUntil);

    /// <summary>Which device got a certificate for a grant (serial only, never the certificate).</summary>
    public sealed record AgentRootIssuedDto(string DeviceName, string Serial);

    /// <summary>A device the requester can choose (active devices only).</summary>
    public sealed record AgentRootDeviceChoice(Guid Id, string Name);

    public sealed record AgentRootCertificateResponse(Guid GrantId, string DeviceName, string Certificate, DateTime ValidBefore, string? Serial);

    public sealed record AddAgentRootDeviceRequest(string? Name, string? PublicKey, string? ClientId);

    public sealed record AgentRootDeviceDto(
        Guid Id, string Name, string Fingerprint, string ClientId, bool IsActive,
        string AddedByEmail, DateTime AddedAt, DateTime? DisabledAt, DateTime? LastFetchedAt);

    internal static async Task<IResult> List(
        HttpContext http, IdentityDbContext db, IOptionsMonitor<AgentRootOptions> options,
        TimeProvider clock, CancellationToken ct)
    {
        var settings = options.CurrentValue;
        var now = clock.GetUtcNow().UtcDateTime;
        await ExpireStaleAsync(db, settings, now, ct);
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
                ApprovedAt = item.ApprovedAt, ValidBefore = item.ValidBefore,
                RequestedDeviceIds = item.RequestedDeviceIds,
                RevokedBy = item.RevokedBy, RevokedAt = item.RevokedAt, FailureReason = item.FailureReason,
            })
            .ToListAsync(ct);
        var issued = await IssuedByGrantAsync(db, grants.Select(item => item.Id).ToArray(), ct);
        var deviceNames = await db.AgentRootDevices.AsNoTracking()
            .ToDictionaryAsync(device => device.Id, device => device.Name, ct);
        var activeDevices = await db.AgentRootDevices.AsNoTracking()
            .Where(device => device.IsActive).OrderBy(device => device.Name)
            .Select(device => new AgentRootDeviceChoice(device.Id, device.Name)).ToArrayAsync(ct);
        return Results.Ok(new
        {
            enabled = settings.Enabled,
            agentId = settings.AgentId,
            principal = settings.Principal,
            minMinutes = settings.MinMinutes,
            maxMinutes = settings.MaxMinutes,
            pendingTtlMinutes = settings.PendingTtlMinutes,
            // The devices a requester can choose from.
            activeDevices,
            items = grants.Select(item => ToDto(item, me, now, settings, issued.GetValueOrDefault(item.Id), deviceNames)).ToArray(),
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
        var requestedIds = (request.DeviceIds ?? []).Distinct().ToArray();
        if (requestedIds.Length == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["deviceIds"] = ["Choose at least one device."] });
        var chosen = await db.AgentRootDevices.AsNoTracking()
            .Where(device => requestedIds.Contains(device.Id) && device.IsActive)
            .OrderBy(device => device.Name)
            .Select(device => new { device.Id, device.Name })
            .ToListAsync(ct);
        if (chosen.Count != requestedIds.Length)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["deviceIds"] = ["Every chosen device must exist and be active."] });
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
            RequestedDeviceIds = JsonSerializer.Serialize(chosen.Select(device => device.Id)),
        };
        db.AgentRootGrants.Add(grant);
        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAuthorizationChangeAsync(db, http, "AGENT_ROOT_GRANT_REQUEST", ResourceType, grant.Id.ToString("D"),
            grant.Reason, null, Snapshot(grant, [], chosen.Select(device => device.Name).ToArray()), ct);
        return Results.Created($"{IdentityApiRoutes.AdminIam}/infra-access/agent-root-grants/{grant.Id:D}",
            ToDto(grant, actor.Id, now, settings, [], chosen.ToDictionary(device => device.Id, device => device.Name)));
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

        // Only the devices the requester chose (still active); grants from before device
        // selection existed sign every active device.
        var chosenIds = ParseDeviceIds(grant.RequestedDeviceIds);
        var devices = await db.AgentRootDevices.AsNoTracking()
            .Where(device => device.IsActive && (chosenIds == null || chosenIds.Contains(device.Id)))
            .OrderBy(device => device.Name).ToListAsync(ct);
        if (devices.Count == 0)
            return Problem(StatusCodes.Status409Conflict, "agent_root_no_devices",
                "None of the chosen devices is active any more; request a new grant.");

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

        var validBefore = now.AddMinutes(grant.DurationMinutes);
        var issued = new List<AgentRootIssuedDto>();
        var failures = new List<string>();
        foreach (var device in devices)
        {
            var keyId = AgentRootGrantRules.BuildKeyId(grant.Id, device.Name, grant.RequestedByEmail, approver.Email);
            try
            {
                var signed = await signer.SignAsync(device.PublicKey, settings.Principal,
                    TimeSpan.FromMinutes(grant.DurationMinutes), keyId, ct);
                var serial = Fit(signed.SerialNumber, 128);
                db.AgentRootCertificates.Add(new AgentRootCertificate
                {
                    Id = Guid.NewGuid(), GrantId = grant.Id, DeviceId = device.Id, DeviceName = device.Name,
                    Certificate = signed.SignedKey, Serial = serial, ValidBefore = validBefore, IssuedAt = now,
                });
                issued.Add(new AgentRootIssuedDto(device.Name, serial));
            }
            catch (AgentRootSigningException exception)
            {
                loggers.CreateLogger(typeof(AgentRootGrantEndpoints)).LogWarning(
                    "Agent root grant {GrantId} could not be signed for device {Device}: {Reason}", grant.Id, device.Name, exception.Message);
                failures.Add($"{device.Name}: {exception.Message}");
            }
        }

        if (issued.Count == 0)
        {
            grant.Status = AgentRootGrant.StatusFailed;
            grant.FailureReason = Fit(string.Join("; ", failures), 500);
            await db.SaveChangesAsync(CancellationToken.None);
            await AdminAudit.LogAuthorizationChangeAsync(db, http, "AGENT_ROOT_GRANT_APPROVE", ResourceType, id.ToString("D"),
                "Agent root grant approved but Vault signing failed.", before, Snapshot(grant, issued), CancellationToken.None);
            return Problem(StatusCodes.Status502BadGateway, "agent_root_sign_failed",
                "Vault could not sign the agent keys. The grant is marked failed; request a new one.");
        }

        grant.Status = AgentRootGrant.StatusIssued;
        grant.ValidBefore = validBefore;
        // Partial success still issues the grant; the devices that failed are named here.
        grant.FailureReason = failures.Count == 0 ? null : Fit("Not signed: " + string.Join("; ", failures), 500);
        await db.SaveChangesAsync(ct);

        // Snapshot carries the serial only; the certificate never goes into audit.
        await AdminAudit.LogAuthorizationChangeAsync(db, http, "AGENT_ROOT_GRANT_APPROVE", ResourceType, id.ToString("D"),
            "Agent root grant approved and certificates issued.", before, Snapshot(grant, issued), ct);
        return Results.Ok(ToDto(grant, approver.Id, clock.GetUtcNow().UtcDateTime, settings, issued,
            devices.ToDictionary(device => device.Id, device => device.Name)));
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
        var issuedBefore = (await IssuedByGrantAsync(db, [id], ct)).GetValueOrDefault(id) ?? [];
        var before = Snapshot(grant, issuedBefore);
        grant.Status = AgentRootGrant.StatusRevoked;
        grant.RevokedBy = actor.Id;
        grant.RevokedAt = now;
        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAuthorizationChangeAsync(db, http, "AGENT_ROOT_GRANT_REVOKE", ResourceType, id.ToString("D"),
            "Agent root grant revoked.", before, Snapshot(grant, issuedBefore), ct);
        return Results.Ok(ToDto(grant, actor.Id, now, options.CurrentValue, issuedBefore, null));
    }

    /// <remarks>
    /// The calling client identifies the device: a device fetches only the certificate signed
    /// for its own key. <c>agentId</c> is accepted for older fetchers and otherwise ignored.
    /// </remarks>
    internal static async Task<IResult> GetCertificate(
        string? agentId, HttpContext http, IdentityDbContext db, IOptionsMonitor<AgentRootOptions> options,
        TimeProvider clock, CancellationToken ct)
    {
        // A certificate is a credential: never cache it anywhere on the way.
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";

        var settings = options.CurrentValue;
        if (agentId is { Length: > 128 })
            return Problem(StatusCodes.Status400BadRequest, "invalid_agent_id", "agentId is too long.");
        if (!settings.Enabled)
            return Problem(StatusCodes.Status404NotFound, "agent_root_disabled", "Agent root access is disabled.");
        var clientId = http.User.FindFirstValue(HisHopeProtocolConstants.Claims.ClientId)
                       ?? http.User.FindFirstValue(HisHopeProtocolConstants.Claims.AuthorizedParty);
        if (string.IsNullOrWhiteSpace(clientId))
            return Results.Forbid();

        var now = clock.GetUtcNow().UtcDateTime;
        await ExpireStaleAsync(db, settings, now, ct);
        var device = await db.AgentRootDevices.AsNoTracking()
            .Where(item => item.ClientId == clientId && item.IsActive)
            .Select(item => new { item.Id, item.Name })
            .FirstOrDefaultAsync(ct);
        if (device is null)
            return Problem(StatusCodes.Status404NotFound, "agent_root_unknown_device", "This client is not an active agent device.");
        await db.AgentRootDevices.Where(item => item.Id == device.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.LastFetchedAt, now), ct);

        var certificate = await db.AgentRootCertificates.AsNoTracking()
            .TagWith("Identity.InfraAccess.AgentRootCertificate")
            .Where(item => item.DeviceId == device.Id && item.ValidBefore > now)
            .Join(db.AgentRootGrants.Where(grant => grant.Status == AgentRootGrant.StatusIssued &&
                                                    grant.RevokedAt == null && grant.ValidBefore > now),
                item => item.GrantId, grant => grant.Id, (item, grant) => item)
            .OrderByDescending(item => item.ValidBefore)
            .Select(item => new { item.GrantId, item.Certificate, item.ValidBefore, item.Serial })
            .FirstOrDefaultAsync(ct);
        if (certificate is null)
            return Problem(StatusCodes.Status404NotFound, "agent_root_no_active_grant", "No active agent root grant.");
        return Results.Ok(new AgentRootCertificateResponse(
            certificate.GrantId, device.Name, certificate.Certificate,
            DateTime.SpecifyKind(certificate.ValidBefore, DateTimeKind.Utc), certificate.Serial));
    }

    internal static async Task<IResult> ListDevices(IdentityDbContext db, CancellationToken ct)
    {
        var devices = await db.AgentRootDevices.AsNoTracking()
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.Name)
            .Take(ListLimit)
            .ToListAsync(ct);
        return Results.Ok(devices.Select(ToDeviceDto).ToArray());
    }

    internal static async Task<IResult> AddDevice(
        AddAgentRootDeviceRequest request, HttpContext http, IdentityDbContext db,
        IOpenIddictApplicationManager applications, TimeProvider clock, CancellationToken ct)
    {
        if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp)
            return stepUp;
        var name = request.Name?.Trim() ?? string.Empty;
        var publicKey = request.PublicKey?.Trim() ?? string.Empty;
        var clientId = request.ClientId?.Trim() ?? string.Empty;
        var errors = new Dictionary<string, string[]>();
        if (!AgentRootOptions.IsValidDeviceName(name))
            errors["name"] = ["Use 1-64 letters, digits, '.', '_' or '-' (starting with a letter or digit)."];
        if (!AgentRootOptions.IsValidPublicKey(publicKey))
            errors["publicKey"] = ["Paste one 'ssh-ed25519 AAAA... comment' line (the .pub file)."];
        if (string.IsNullOrWhiteSpace(clientId) || clientId.Length > 128)
            errors["clientId"] = ["The client id is required."];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);
        if (await applications.FindByClientIdAsync(clientId, ct) is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["clientId"] = [$"Client '{clientId}' does not exist."] });

        var fingerprint = AgentRootOptions.Fingerprint(publicKey);
        if (await db.AgentRootDevices.AnyAsync(item => item.Name == name, ct))
            return Problem(StatusCodes.Status409Conflict, "agent_root_device_exists", $"A device named '{name}' already exists.");
        if (await db.AgentRootDevices.AnyAsync(item => item.Fingerprint == fingerprint, ct))
            return Problem(StatusCodes.Status409Conflict, "agent_root_key_in_use", "This public key is already registered.");
        if (await db.AgentRootDevices.AnyAsync(item => item.ClientId == clientId && item.IsActive, ct))
            return Problem(StatusCodes.Status409Conflict, "agent_root_client_in_use",
                "That client already belongs to an active device; each device needs its own client.");
        if (await ResolveActorAsync(http, db, ct) is not { } actor)
            return Results.Forbid();

        var device = new AgentRootDevice
        {
            Id = Guid.NewGuid(), Name = name, PublicKey = publicKey, Fingerprint = fingerprint, ClientId = clientId,
            IsActive = true, AddedBy = actor.Id, AddedByEmail = actor.Email, AddedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.AgentRootDevices.Add(device);
        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAuthorizationChangeAsync(db, http, "AGENT_ROOT_DEVICE_ADD", DeviceResourceType, device.Id.ToString("D"),
            $"Agent root device '{name}' added.", null, DeviceSnapshot(device), ct);
        return Results.Created($"{IdentityApiRoutes.AdminIam}/infra-access/agent-root-devices/{device.Id:D}", ToDeviceDto(device));
    }

    private static async Task<IResult> SetDeviceActiveAsync(
        Guid id, bool active, HttpContext http, IdentityDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp)
            return stepUp;
        if (await ResolveActorAsync(http, db, ct) is not { } actor)
            return Results.Forbid();
        var device = await db.AgentRootDevices.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (device is null)
            return Problem(StatusCodes.Status404NotFound, "agent_root_device_not_found", "The agent device does not exist.");
        if (device.IsActive == active)
            return Results.Ok(ToDeviceDto(device));
        if (active && await db.AgentRootDevices.AnyAsync(item => item.Id != id && item.ClientId == device.ClientId && item.IsActive, ct))
            return Problem(StatusCodes.Status409Conflict, "agent_root_client_in_use",
                "Another active device uses the same client.");

        var before = DeviceSnapshot(device);
        device.IsActive = active;
        device.DisabledBy = active ? null : actor.Id;
        device.DisabledAt = active ? null : clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        await AdminAudit.LogAuthorizationChangeAsync(db, http, active ? "AGENT_ROOT_DEVICE_ENABLE" : "AGENT_ROOT_DEVICE_DISABLE",
            DeviceResourceType, id.ToString("D"),
            active ? $"Agent root device '{device.Name}' enabled." : $"Agent root device '{device.Name}' disabled; it can no longer fetch certificates.",
            before, DeviceSnapshot(device), ct);
        return Results.Ok(ToDeviceDto(device));
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<AgentRootIssuedDto>>> IssuedByGrantAsync(
        IdentityDbContext db, Guid[] grantIds, CancellationToken ct)
    {
        if (grantIds.Length == 0) return [];
        var rows = await db.AgentRootCertificates.AsNoTracking()
            .Where(item => grantIds.Contains(item.GrantId))
            .OrderBy(item => item.DeviceName)
            // Serial and device only; the certificate column is never read here.
            .Select(item => new { item.GrantId, item.DeviceName, item.Serial })
            .ToListAsync(ct);
        return rows.GroupBy(item => item.GrantId).ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<AgentRootIssuedDto>)group.Select(item => new AgentRootIssuedDto(item.DeviceName, item.Serial)).ToArray());
    }

    /// <summary>Marks issued grants past ValidBefore and pending grants past the approval window as expired.</summary>
    private static async Task ExpireStaleAsync(IdentityDbContext db, AgentRootOptions settings, DateTime now, CancellationToken ct)
    {
        var pendingCutoff = now.AddMinutes(-settings.PendingTtlMinutes);
        await db.AgentRootGrants
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
        var email = stored?.Email ?? stored?.UserName ?? http.User.FindFirstValue(HisHopeProtocolConstants.Claims.Email) ??
                    http.User.FindFirstValue(ClaimTypes.Email) ?? id.ToString("D");
        return new Actor(id, Fit(email, 256));
    }

    private static AgentRootGrantDto ToDto(
        AgentRootGrant item, Guid? me, DateTime now, AgentRootOptions settings, IReadOnlyList<AgentRootIssuedDto>? issued,
        IReadOnlyDictionary<Guid, string>? deviceNames) => new(
        item.Id, item.AgentId, item.Reason, item.DurationMinutes, item.Status,
        item.RequestedBy, item.RequestedByEmail, Utc(item.RequestedAt)!.Value,
        item.ApprovedBy, item.ApprovedByEmail, Utc(item.ApprovedAt),
        Utc(item.ValidBefore),
        (ParseDeviceIds(item.RequestedDeviceIds) ?? [])
            .Select(id => deviceNames is not null && deviceNames.TryGetValue(id, out var name) ? name : id.ToString("D"))
            .Order(StringComparer.Ordinal).ToArray(),
        issued ?? [],
        item.RevokedBy, Utc(item.RevokedAt), item.FailureReason,
        AgentRootGrantRules.IsActive(item, now), AgentRootGrantRules.RemainingMinutes(item, now),
        me is { } current && current == item.RequestedBy,
        item.Status == AgentRootGrant.StatusPending ? Utc(item.RequestedAt.AddMinutes(settings.PendingTtlMinutes)) : null);

    private static Guid[]? ParseDeviceIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<Guid[]>(json); }
        catch (JsonException) { return []; }
    }

    private static AgentRootDeviceDto ToDeviceDto(AgentRootDevice item) => new(
        item.Id, item.Name, item.Fingerprint, item.ClientId, item.IsActive, item.AddedByEmail,
        DateTime.SpecifyKind(item.AddedAt, DateTimeKind.Utc), Utc(item.DisabledAt), Utc(item.LastFetchedAt));

    private static DateTime? Utc(DateTime? value) => value is { } v ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : null;

    /// <summary>Audit snapshot: everything except certificate text (device names and serials only).</summary>
    private static string Snapshot(AgentRootGrant item, IReadOnlyList<AgentRootIssuedDto> issued, IReadOnlyList<string>? requestedDevices = null) => JsonSerializer.Serialize(new
    {
        item.Id, item.AgentId, item.Reason, item.DurationMinutes, item.Status,
        item.RequestedDeviceIds, RequestedDevices = requestedDevices,
        item.RequestedBy, item.RequestedByEmail, item.RequestedAt,
        item.ApprovedBy, item.ApprovedByEmail, item.ApprovedAt,
        item.ValidBefore, Certificates = issued, item.RevokedBy, item.RevokedAt, item.FailureReason,
    });

    private static string Snapshot(AgentRootGrant item) => Snapshot(item, []);

    private static string DeviceSnapshot(AgentRootDevice item) => JsonSerializer.Serialize(new
    {
        item.Id, item.Name, item.Fingerprint, item.ClientId, item.IsActive, item.AddedBy, item.AddedByEmail,
        item.AddedAt, item.DisabledBy, item.DisabledAt,
    });

    private static string Fit(string value, int max) => value.Length <= max ? value : value[..max];

    private static IResult Disabled() =>
        Problem(StatusCodes.Status503ServiceUnavailable, "agent_root_disabled", "Agent root access is disabled on this host.");

    private static IResult Problem(int status, string code, string detail) =>
        Results.Problem(statusCode: status, detail: detail,
            extensions: new Dictionary<string, object?> { ["errorCode"] = code });
}

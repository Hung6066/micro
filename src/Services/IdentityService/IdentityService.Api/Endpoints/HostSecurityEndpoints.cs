using System.Globalization;
using His.Hope.IdentityService.Api.HostSecurity;
using His.Hope.IdentityService.Api.InfraAudit;
using His.Hope.IdentityService.Application.HostSecurity;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// Security page (super-admin only, on the /api/v1/admin/iam group): host integrity drift,
/// sudo, console logins and Falco alerts, live from Loki, plus acknowledgements.
/// Every read and acknowledgement is audited.
/// </summary>
public static class HostSecurityEndpoints
{
    public const int MaxAckBatch = 100;

    public static void MapHostSecurityEndpoints(this RouteGroupBuilder iam)
    {
        var group = iam.MapGroup("/host-security");
        group.MapGet("/meta", Meta);
        group.MapGet("/events", Events);
        group.MapGet("/hosts", Hosts);
        group.MapPost("/acks", Acknowledge);
        group.MapGet("/approvals", Approvals);
        group.MapPost("/approvals", Approve);
    }

    /// <summary>Machine endpoint: the integrity check on each host pulls its approved drift fingerprints.</summary>
    public static void MapHostIntegrityApprovalPullEndpoint(this WebApplication app) =>
        app.MapGet(His.Hope.Contracts.Identity.IdentityApiRoutes.OpenShipHostIntegrityApprovals, PullApprovals)
            .RequireAuthorization(His.Hope.IdentityService.Api.Composition.IdentityServiceRegistrationExtensions.OpenShipHostIntegrityApprovalsPolicy);

    private static IResult Meta(HostSecurityService service) => Results.Ok(new
    {
        configured = service.IsConfigured,
        categories = HostSecurityCategories.All,
        severities = HostSecuritySeverities.All,
        maxLimit = HostSecurityService.MaxLimit,
        maxRangeDays = HostSecurityService.MaxRangeDays,
        staleAfterMinutes = (int)HostSecurityService.StaleAfter.TotalMinutes,
    });

    private static async Task<IResult> Events(
        DateTimeOffset? from, DateTimeOffset? to, string? categories, string? minSeverity, string? host, string? text,
        bool? unacknowledgedOnly, int? limit, HostSecurityService service, IAuditService audit, HttpContext http, CancellationToken ct)
    {
        if (service.TryCreateFilter(from, to, categories, minSeverity, host, text, unacknowledgedOnly, limit, out var filter) is { } error)
            return Invalid(error);
        return await Guarded(async () =>
        {
            var page = await service.GetPageAsync(filter, ct);
            await AdminAudit.LogAsync(audit, http, "VIEW", "HostSecurity", Fit(string.Create(CultureInfo.InvariantCulture,
                $"{filter.From.UtcDateTime:yyyyMMdd'T'HHmm}-{filter.To.UtcDateTime:yyyyMMdd'T'HHmm} c={string.Join('|', filter.Categories)} s={filter.MinSeverity} h={filter.Host}")), ct);
            return Results.Ok(new
            {
                items = page.Items,
                unacknowledged = page.UnacknowledgedBySeverity,
                truncated = page.Truncated,
                unavailableSources = page.UnavailableSources,
            });
        });
    }

    private static async Task<IResult> Hosts(HostSecurityService service, IAuditService audit, HttpContext http, CancellationToken ct) =>
        await Guarded(async () =>
        {
            var hosts = await service.GetHostStatusAsync(ct);
            await AdminAudit.LogAsync(audit, http, "VIEW", "HostSecurityHosts", $"hosts={hosts.Count}", ct);
            return Results.Ok(new { items = hosts });
        });

    public sealed record AckItem(string Key, string? Severity, string? Host, string? SecurityEvent);

    public sealed record AckRequest(IReadOnlyList<AckItem>? Items, string? Note);

    private static async Task<IResult> Acknowledge(AckRequest request, IdentityDbContext db, IHostSecurityAckStore store, HttpContext http, CancellationToken ct)
    {
        var items = request.Items ?? [];
        if (items.Count is 0 or > MaxAckBatch) return Invalid("invalid_batch");
        if (request.Note is { Length: > 500 }) return Invalid("note_too_long");
        if (items.Any(item => !IsAckKey(item.Key))) return Invalid("invalid_key");
        var userId = http.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value;
        if (string.IsNullOrEmpty(userId)) return Results.Forbid();

        var keys = items.Select(item => item.Key).Distinct(StringComparer.Ordinal).ToList();
        var existing = await store.GetAsync(keys, ct);
        var added = 0;
        foreach (var item in items.DistinctBy(item => item.Key))
        {
            if (existing.ContainsKey(item.Key)) continue;
            db.AuditLogs.Add(AuditLogHostSecurityAckStore.Create(item.Key, userId, http.User.Identity?.Name, request.Note?.Trim(),
                Clip(item.Severity, 16), Clip(item.Host, 64), Clip(item.SecurityEvent, 64),
                http.Connection.RemoteIpAddress?.ToString(), http.Request.Headers.UserAgent.ToString(), http.TraceIdentifier));
            added++;
        }
        if (added > 0) await db.SaveChangesAsync(ct);
        var acks = await store.GetAsync(keys, ct);
        return Results.Ok(new { acknowledged = added, items = acks.Values });
    }

    public sealed record ApproveRequest(string? Host, string? Fingerprint, string? Reason);

    private static async Task<IResult> Approvals(HostIntegrityApprovalStore store, TimeProvider clock, CancellationToken ct) =>
        Results.Ok(new { items = await store.RecentAsync(clock.GetUtcNow() - HostIntegrityApprovalRules.HostPullWindow, ct) });

    /// <summary>
    /// Approve a drift (fresh MFA). Warning drift is approved at once; critical drift needs a second,
    /// different super-admin: the first call records a request, the second approves.
    /// </summary>
    private static async Task<IResult> Approve(
        ApproveRequest request, HostSecurityService service, HostIntegrityApprovalStore store, IdentityDbContext db,
        TimeProvider clock, HttpContext http, CancellationToken ct)
    {
        if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp)
            return stepUp;
        if (HostIntegrityApprovalRules.Validate(request.Host, request.Fingerprint, request.Reason) is { } error)
            return Invalid(error);
        var subject = http.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value;
        if (!Guid.TryParse(subject, out var userId)) return Results.Forbid();
        var user = await db.Users.AsNoTracking().Where(item => item.Id == userId)
            .Select(item => new { item.Email, item.UserName }).FirstOrDefaultAsync(ct);
        var userName = user?.Email ?? user?.UserName ?? userId.ToString("D");
        var host = request.Host!.Trim();
        var fingerprint = request.Fingerprint!.Trim();
        var reason = request.Reason!.Trim();
        var key = HostIntegrityApprovalRules.Key(host, fingerprint);

        return await Guarded(async () =>
        {
            var drift = await service.FindDriftAsync(host, fingerprint, ct);
            if (drift is null)
                return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Drift not found",
                    extensions: new Dictionary<string, object?> { ["errorCode"] = "drift_not_found",
                        ["reason"] = "No drift with this fingerprint on this host in the last 24 hours (already approved or changed again)." });
            var state = await store.GetStateAsync(key, ct);
            var decision = HostIntegrityApprovalRules.Decide(state, userId, drift.Severity, clock.GetUtcNow());
            switch (decision)
            {
                case HostIntegrityApprovalDecision.AlreadyApproved:
                    return Results.Ok(new { status = "approved", key });
                case HostIntegrityApprovalDecision.SameApprover:
                    return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Second approver required",
                        extensions: new Dictionary<string, object?> { ["errorCode"] = "second_approver_required",
                            ["reason"] = "Critical drift needs a different super-admin to confirm your request." });
                case HostIntegrityApprovalDecision.Request:
                    store.Add(HostIntegrityApprovalStore.Requested, key, userId, userName, reason, drift.Severity, host, fingerprint, drift.Changes.Count, http);
                    await db.SaveChangesAsync(ct);
                    return Results.Accepted(value: new { status = "pending_second_approver", key });
                default:
                    store.Add(HostIntegrityApprovalStore.Approved, key, userId, userName, reason, drift.Severity, host, fingerprint, drift.Changes.Count, http);
                    await db.SaveChangesAsync(ct);
                    return Results.Ok(new { status = "approved", key });
            }
        });
    }

    private static async Task<IResult> PullApprovals(string? host, HostIntegrityApprovalStore store, TimeProvider clock, CancellationToken ct)
    {
        if (!HostIntegrityApprovalRules.IsValidHost(host)) return Invalid("invalid_host");
        var items = await store.ApprovedForHostAsync(host!.Trim(), clock.GetUtcNow() - HostIntegrityApprovalRules.HostPullWindow, ct);
        return Results.Ok(new
        {
            host,
            items = items.Select(item => new { item.Fingerprint, item.Reason, item.RequestedBy, item.ApprovedBy, item.ApprovedAt, item.Severity }),
        });
    }

    /// <summary>Keys the normalizer produces: "evt:&lt;20 hex&gt;" or "drift:&lt;host&gt;:&lt;16 hex&gt;".</summary>
    internal static bool IsAckKey(string? key) =>
        key is { Length: > 0 and <= 100 } &&
        (System.Text.RegularExpressions.Regex.IsMatch(key, "^evt:[0-9a-f]{20}$") ||
         System.Text.RegularExpressions.Regex.IsMatch(key, "^drift:[A-Za-z0-9][A-Za-z0-9.-]{0,62}:[0-9a-f]{16}$"));

    private static string Clip(string? value, int max) => string.IsNullOrEmpty(value) ? "" : value.Length <= max ? value : value[..max];

    private static string Fit(string value) => value.Length <= 100 ? value : value[..100];

    private static async Task<IResult> Guarded(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (LokiUnavailableException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Security events unavailable",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "host_security_unavailable", ["reason"] = exception.Reason });
        }
    }

    private static IResult Invalid(string code) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid security events request",
            extensions: new Dictionary<string, object?> { ["errorCode"] = "invalid_host_security_request", ["reason"] = code });
}

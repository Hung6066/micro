using System.Globalization;
using System.Text;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Api.InfraAudit;
using His.Hope.IdentityService.Application.InfraAudit;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// Infrastructure access audit (plan stage 6A). Everything under /admin/iam/infra-audit is
/// super-admin only (HumanSuperAdmin on the group); export also needs a fresh MFA. Any admin
/// may read their own activity under /admin/infra-audit/me. Every read is itself audited.
/// </summary>
public static class InfraAuditEndpoints
{
    /// <summary>Maps on the super-admin /api/v1/admin/iam group.</summary>
    public static void MapInfraAuditEndpoints(this RouteGroupBuilder iam)
    {
        var group = iam.MapGroup("/infra-audit");
        group.MapGet("/meta", Meta);
        group.MapGet("/events", Events);
        group.MapGet("/summary", Summary);
        group.MapGet("/export", Export);
        group.MapGet("/grants/{id:guid}/timeline", GrantTimeline);
        group.MapGet("/warpgate-sessions", WarpgateSessions);
    }

    /// <summary>Maps "my activity" on the human-admin /api/v1/admin group.</summary>
    public static void MapMyInfraAuditEndpoints(this RouteGroupBuilder admin) =>
        admin.MapGet("/infra-audit/me/events", MyEvents);

    private static IResult Meta(InfraAuditService service) => Results.Ok(new
    {
        configured = service.IsConfigured,
        sources = InfraAuditQuery.Sources,
        results = InfraAuditResults.All,
        retentionDays = service.Options.RetentionDays,
        maxRangeDays = service.Options.MaxRangeDays,
        maxPageSize = service.Options.MaxPageSize,
        maxExportRows = service.Options.MaxExportRows,
    });

    private static async Task<IResult> Events(
        DateTimeOffset? from, DateTimeOffset? to, string? sources, string? actor, string? text,
        string? result, int? limit, InfraAuditService service, TimeProvider clock,
        IAuditService audit, HttpContext http, CancellationToken ct)
    {
        if (InfraAuditQuery.TryCreateFilter(clock.GetUtcNow(), service.Options, from, to, sources, actor, text, result, limit, out var filter) is { } error)
            return Invalid(error);
        return await Guarded(async () =>
        {
            var page = await service.GetPageAsync(filter, ct);
            await AdminAudit.LogAsync(audit, http, "VIEW", "InfraAudit", Describe(filter), ct);
            return Results.Ok(new { items = page.Items, nextTo = page.NextTo });
        });
    }

    private static async Task<IResult> Summary(
        DateTimeOffset? from, DateTimeOffset? to, string? sources, InfraAuditService service, TimeProvider clock,
        IAuditService audit, HttpContext http, CancellationToken ct)
    {
        if (InfraAuditQuery.TryCreateFilter(clock.GetUtcNow(), service.Options, from, to, sources, null, null, null, null, out var filter) is { } error)
            return Invalid(error);
        return await Guarded(async () =>
        {
            var summary = await service.SummarizeAsync(filter, ct);
            await AdminAudit.LogAsync(audit, http, "VIEW", "InfraAuditSummary", Describe(filter), ct);
            return Results.Ok(summary);
        });
    }

    private static async Task<IResult> Export(
        DateTimeOffset? from, DateTimeOffset? to, string? sources, string? actor, string? text,
        string? result, InfraAuditService service, TimeProvider clock,
        IAuditService audit, HttpContext http, CancellationToken ct)
    {
        // Export takes data out of the system: require a fresh MFA even though it is a GET.
        if (StepUpAuthenticationGuard.RequireFreshMfa(http) is { } stepUp)
            return stepUp;
        if (InfraAuditQuery.TryCreateFilter(clock.GetUtcNow(), service.Options, from, to, sources, actor, text, result, null, out var filter) is { } error)
            return Invalid(error);
        return await Guarded(async () =>
        {
            var rows = await service.ExportAsync(filter, ct);
            await AdminAudit.LogAsync(audit, http, "EXPORT", "InfraAudit", Fit($"rows={rows.Count} {Describe(filter)}"), ct);
            var name = $"infra-audit-{filter.From:yyyyMMddHHmm}-{filter.To:yyyyMMddHHmm}.csv";
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ToCsv(rows))).ToArray(),
                "text/csv; charset=utf-8", name);
        });
    }

    private static async Task<IResult> GrantTimeline(
        Guid id, IdentityDbContext db, InfraAuditService service, TimeProvider clock,
        IAuditService audit, HttpContext http, CancellationToken ct)
    {
        var grant = await db.BreakGlassRequests.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (grant is null) return Results.NotFound();
        var subject = await db.Users.AsNoTracking()
            .Where(user => user.Id == grant.SubjectUserId)
            .Select(user => new { user.Email, user.UserName })
            .FirstOrDefaultAsync(ct);

        var now = clock.GetUtcNow();
        var start = new DateTimeOffset(DateTime.SpecifyKind(grant.ApprovedAt ?? grant.RequestedAt, DateTimeKind.Utc));
        var endUtc = grant.RevokedAt is { } revoked && revoked < grant.ExpiresAt ? revoked : grant.ExpiresAt;
        var end = new DateTimeOffset(DateTime.SpecifyKind(endUtc, DateTimeKind.Utc));
        if (end > now) end = now;
        if (end <= start) end = start.AddMinutes(1);
        if (start < now.AddDays(-service.Options.RetentionDays))
            return Results.Ok(new { grantId = id, from = start, to = end, items = Array.Empty<InfraAuditEvent>(), expired = true });

        var actors = new[] { subject?.Email, subject?.UserName, grant.SubjectUserId.ToString("D") }
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var filter = new InfraAuditFilter(start, end, [], null, null, null, service.Options.MaxPageSize, actors);
        return await Guarded(async () =>
        {
            var page = await service.GetPageAsync(filter, ct);
            await AdminAudit.LogAsync(audit, http, "VIEW", "InfraAuditGrantTimeline", id.ToString("D"), ct);
            return Results.Ok(new { grantId = id, from = start, to = end, items = page.Items, truncated = page.NextTo is not null });
        });
    }

    private static async Task<IResult> WarpgateSessions(
        int? hours, InfraAuditService service, TimeProvider clock,
        IAuditService audit, HttpContext http, CancellationToken ct)
    {
        if (WarpgateSessionTracker.TryCreateWindow(clock.GetUtcNow(), service.Options, hours, out var from, out var to) is { } error)
            return Invalid(error);
        return await Guarded(async () =>
        {
            var sessions = await service.GetOpenWarpgateSessionsAsync(from, to, ct);
            await AdminAudit.LogAsync(audit, http, "VIEW", "InfraAuditWarpgateSessions",
                string.Create(CultureInfo.InvariantCulture, $"hours={hours ?? WarpgateSessionTracker.DefaultHours} open={sessions.Items.Count}"), ct);
            return Results.Ok(new { from, to, items = sessions.Items, truncated = sessions.Truncated });
        });
    }

    private static async Task<IResult> MyEvents(
        DateTimeOffset? from, DateTimeOffset? to, string? sources, int? limit,
        IdentityDbContext db, InfraAuditService service, TimeProvider clock,
        IAuditService audit, HttpContext http, CancellationToken ct)
    {
        var subject = http.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value;
        if (!Guid.TryParse(subject, out var userId)) return Results.Forbid();
        var user = await db.Users.AsNoTracking().Where(item => item.Id == userId)
            .Select(item => new { item.Email, item.UserName }).FirstOrDefaultAsync(ct);
        if (InfraAuditQuery.TryCreateFilter(clock.GetUtcNow(), service.Options, from, to, sources, null, null, null, limit, out var filter) is { } error)
            return Invalid(error);
        var actors = new[] { user?.Email, user?.UserName, userId.ToString("D") }
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        filter = filter with { ActorAny = actors };
        return await Guarded(async () =>
        {
            var page = await service.GetPageAsync(filter, ct);
            await AdminAudit.LogAsync(audit, http, "VIEW", "InfraAuditSelf", Describe(filter), ct);
            return Results.Ok(new { items = page.Items, nextTo = page.NextTo });
        });
    }

    private static async Task<IResult> Guarded(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (InfraAuditUnavailableException exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Infrastructure audit unavailable",
                extensions: new Dictionary<string, object?>
                {
                    ["errorCode"] = "infra_audit_unavailable",
                    ["reason"] = exception.Reason,
                });
        }
    }

    private static IResult Invalid(string code) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid infrastructure audit query",
            extensions: new Dictionary<string, object?> { ["errorCode"] = "invalid_infra_audit_query", ["reason"] = code });

    /// <summary>Compact filter description for the audit row; audit_logs.resource_id is varchar(100).</summary>
    internal static string Describe(InfraAuditFilter filter)
    {
        var value = string.Create(CultureInfo.InvariantCulture,
            $"{filter.From.UtcDateTime:yyyyMMdd'T'HHmm}-{filter.To.UtcDateTime:yyyyMMdd'T'HHmm} s={string.Join('|', filter.Sources)} a={filter.Actor} t={filter.Text} r={filter.Result}");
        return Fit(value);
    }

    private static string Fit(string value) => value.Length <= 100 ? value : value[..100];

    /// <summary>RFC 4180 CSV; cells starting with = + - @ are prefixed to stop formula injection.</summary>
    public static string ToCsv(IEnumerable<InfraAuditEvent> rows)
    {
        var builder = new StringBuilder("timestamp,source,actor,action,target,result,sourceIp,correlationId,detail,logType\r\n");
        foreach (var row in rows)
        {
            builder.Append(string.Join(',', new[]
            {
                row.Timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), row.Source, row.Actor, row.Action,
                row.Target, row.Result, row.SourceIp, row.CorrelationId, row.Detail, row.LogType,
            }.Select(Cell))).Append("\r\n");
        }
        return builder.ToString();
    }

    private static string Cell(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
    }
}

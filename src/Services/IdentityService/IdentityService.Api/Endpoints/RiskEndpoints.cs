using System.Globalization;
using His.Hope.IdentityService.Application.Risk;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// IAM → Risk (Zero Trust 3.5 R0, super-admin only, on the /api/v1/admin/iam group):
/// scored sign-in events and report-only policy counts. Every read is audited.
/// </summary>
public static class RiskEndpoints
{
    public const int MaxPageSize = 200;
    public const int MaxRangeDays = 31;

    public static void MapRiskEndpoints(this RouteGroupBuilder iam)
    {
        var group = iam.MapGroup("/risk");
        group.MapGet("/events", Events);
        group.MapGet("/summary", Summary);
    }

    public sealed record RiskEventItem(
        Guid Id,
        DateTime Timestamp,
        Guid? UserId,
        string? UserName,
        string EventType,
        string? Severity,
        string? IpPrefix,
        int? Score,
        string? Band,
        IReadOnlyList<RiskEventReason> Reasons,
        IReadOnlyList<string> Methods,
        string? Outcome);

    private static async Task<IResult> Events(
        string? band, string? user, DateTimeOffset? from, DateTimeOffset? to, int? page, int? pageSize,
        IdentityDbContext db, TimeProvider clock, IAuditService audit, HttpContext http, CancellationToken ct)
    {
        RiskBand? bandFilter = null;
        if (!string.IsNullOrWhiteSpace(band))
        {
            if (!RiskBands.TryParse(band, out var parsed)) return Invalid("invalid_band");
            bandFilter = parsed;
        }
        var now = clock.GetUtcNow();
        var until = to ?? now;
        var since = from ?? until.AddDays(-7);
        if (since >= until) return Invalid("invalid_range");
        if (until - since > TimeSpan.FromDays(MaxRangeDays)) return Invalid("range_too_large");
        var pageNumber = page ?? 1;
        var size = pageSize ?? 50;
        if (pageNumber < 1) return Invalid("invalid_page");
        if (size is < 1 or > MaxPageSize) return Invalid("invalid_page_size");
        var userFilter = user?.Trim();
        if (userFilter is { Length: > 256 }) return Invalid("invalid_user");

        var types = RiskEventTypes.Scored.Concat(RiskEventTypes.WouldAct).ToArray();
        var sinceUtc = since.UtcDateTime;
        var untilUtc = until.UtcDateTime;
        var query = db.SecurityEvents.AsNoTracking()
            .Where(item => types.Contains(item.EventType) && item.Timestamp >= sinceUtc && item.Timestamp < untilUtc
                && item.Details != null && item.Details.Contains("\"risk\":"));
        if (bandFilter is { } wanted)
        {
            var marker = RiskEventDetails.BandMarker(wanted);
            query = query.Where(item => item.Details!.Contains(marker));
        }
        if (!string.IsNullOrEmpty(userFilter))
        {
            if (Guid.TryParse(userFilter, out var userId))
                query = query.Where(item => item.UserId == userId);
            else
            {
                var lowered = userFilter.ToLowerInvariant();
                query = query.Where(item => item.UserName != null && item.UserName.ToLower().Contains(lowered));
            }
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(item => item.Timestamp).ThenBy(item => item.Id)
            .Skip((pageNumber - 1) * size).Take(size)
            .Select(item => new { item.Id, item.Timestamp, item.UserId, item.UserName, item.EventType, item.Severity, item.IpAddress, item.Details })
            .ToListAsync(ct);
        var items = rows.Select(row =>
        {
            var details = RiskEventDetails.TryParse(row.Details);
            return new RiskEventItem(row.Id, DateTime.SpecifyKind(row.Timestamp, DateTimeKind.Utc), row.UserId, row.UserName, row.EventType,
                row.Severity, details?.IpPrefix ?? row.IpAddress, details?.Risk.Score, details?.Risk.Band,
                details?.Risk.Reasons ?? [], details?.Methods ?? [], details?.Outcome);
        }).ToList();

        await AdminAudit.LogAsync(audit, http, "VIEW", "RiskEvents", Fit(string.Create(CultureInfo.InvariantCulture,
            $"{sinceUtc:yyyyMMdd'T'HHmm}-{untilUtc:yyyyMMdd'T'HHmm} b={(bandFilter is null ? "" : RiskBands.Name(bandFilter.Value))} u={(string.IsNullOrEmpty(userFilter) ? "" : "set")} p={pageNumber}")), ct);
        return Results.Ok(new { items, totalCount = total, page = pageNumber, pageSize = size });
    }

    private static async Task<IResult> Summary(IdentityDbContext db, TimeProvider clock, IAuditService audit, HttpContext http, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var windows = new[] { ("24h", now.AddHours(-24)), ("7d", now.AddDays(-7)) };
        var scored = RiskEventTypes.Scored.ToArray();
        var wouldAct = RiskEventTypes.WouldAct.ToArray();
        var result = new Dictionary<string, object>();
        foreach (var (name, since) in windows)
        {
            var events = db.SecurityEvents.AsNoTracking()
                .Where(item => item.Timestamp >= since && item.Details != null && item.Details.Contains("\"risk\":"));
            var scoredEvents = events.Where(item => scored.Contains(item.EventType));
            var bands = new Dictionary<string, int>();
            foreach (var band in Enum.GetValues<RiskBand>())
            {
                var marker = RiskEventDetails.BandMarker(band);
                bands[RiskBands.Name(band)] = await scoredEvents.CountAsync(item => item.Details!.Contains(marker), ct);
            }
            var acts = await events.Where(item => wouldAct.Contains(item.EventType))
                .GroupBy(item => item.EventType)
                .Select(group => new { group.Key, Count = group.Count() })
                .ToListAsync(ct);
            result[name] = new
            {
                bands,
                total = bands.Values.Sum(),
                wouldStepUp = acts.FirstOrDefault(item => item.Key == RiskEventTypes.WouldStepUp)?.Count ?? 0,
                wouldDeny = acts.FirstOrDefault(item => item.Key == RiskEventTypes.WouldDeny)?.Count ?? 0,
            };
        }

        await AdminAudit.LogAsync(audit, http, "VIEW", "RiskSummary", "24h,7d", ct);
        return Results.Ok(new { generatedAt = DateTime.SpecifyKind(now, DateTimeKind.Utc), windows = result, reportOnly = true });
    }

    private static string Fit(string value) => value.Length <= 100 ? value : value[..100];

    private static IResult Invalid(string code) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid risk events request",
            extensions: new Dictionary<string, object?> { ["errorCode"] = "invalid_risk_request", ["reason"] = code });
}

using System.Text.Json;
using His.Hope.IdentityService.Application.HostSecurity;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.HostSecurity;

/// <summary>
/// Drift approvals as audit_logs rows (ResourceType HostIntegrityDrift, ResourceId = the drift
/// key "drift:&lt;host&gt;:&lt;fingerprint&gt;"): HOST_INTEGRITY_APPROVAL_REQUESTED by the first
/// super-admin for critical drift, HOST_INTEGRITY_APPROVED when it takes effect. Append-only and
/// covered by the audit hash chain.
/// </summary>
public sealed class HostIntegrityApprovalStore(IdentityDbContext db)
{
    public const string ResourceType = "HostIntegrityDrift";
    public const string Requested = "HOST_INTEGRITY_APPROVAL_REQUESTED";
    public const string Approved = "HOST_INTEGRITY_APPROVED";

    private sealed record Details(string? Reason, string? Severity, string? Host, string? Fingerprint, int? ChangeCount);

    public async Task<HostIntegrityApprovalState> GetStateAsync(string key, CancellationToken ct)
    {
        var rows = await Rows(db.AuditLogs.AsNoTracking().Where(row => row.ResourceId == key), ct);
        return new HostIntegrityApprovalState(
            rows.Where(row => row.Action == Requested).OrderByDescending(row => row.Timestamp).Select(Step).FirstOrDefault(),
            rows.Where(row => row.Action == Approved).OrderBy(row => row.Timestamp).Select(Step).FirstOrDefault());
    }

    /// <summary>Approved drifts of one host since <paramref name="since"/> (what the host pulls).</summary>
    public async Task<IReadOnlyList<HostIntegrityApproval>> ApprovedForHostAsync(string host, DateTimeOffset since, CancellationToken ct)
    {
        var prefix = $"drift:{host}:";
        var cutoff = since.UtcDateTime;
        var rows = await Rows(db.AuditLogs.AsNoTracking()
            .Where(row => row.Action == Approved && row.Timestamp >= cutoff && row.ResourceId!.StartsWith(prefix)), ct);
        return Merge(rows, await RequestsFor(rows.Select(row => row.ResourceId!).ToList(), ct))
            .Where(item => item.Status == "approved").ToList();
    }

    /// <summary>Requests and approvals since <paramref name="since"/> (admin page).</summary>
    public async Task<IReadOnlyList<HostIntegrityApproval>> RecentAsync(DateTimeOffset since, CancellationToken ct)
    {
        var cutoff = since.UtcDateTime;
        var rows = await Rows(db.AuditLogs.AsNoTracking()
            .Where(row => (row.Action == Approved || row.Action == Requested) && row.Timestamp >= cutoff).Take(1000), ct);
        return Merge(rows.Where(row => row.Action == Approved).ToList(), rows.Where(row => row.Action == Requested).ToList())
            .OrderByDescending(item => item.ApprovedAt ?? item.RequestedAt).ToList();
    }

    public void Add(string action, string key, Guid userId, string userName, string reason, string severity, string host,
        string fingerprint, int changeCount, HttpContext http)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId.ToString("D"),
            UserName = userName,
            Action = action,
            ResourceType = ResourceType,
            ResourceId = key,
            Details = JsonSerializer.Serialize(new Details(reason, severity, host, fingerprint, changeCount)),
            CorrelationId = http.TraceIdentifier,
            Outcome = "success",
            Source = "host-security",
            IpAddress = http.Connection.RemoteIpAddress?.ToString(),
            UserAgent = http.Request.Headers.UserAgent.ToString() is { Length: > 500 } agent ? agent[..500] : http.Request.Headers.UserAgent.ToString(),
        });
    }

    private async Task<List<AuditLog>> RequestsFor(IReadOnlyCollection<string> keys, CancellationToken ct) =>
        keys.Count == 0 ? [] : await Rows(db.AuditLogs.AsNoTracking().Where(row => row.Action == Requested && keys.Contains(row.ResourceId!)), ct);

    private static Task<List<AuditLog>> Rows(IQueryable<AuditLog> query, CancellationToken ct) =>
        query.Where(row => row.ResourceType == ResourceType && row.ResourceId != null).ToListAsync(ct);

    private static IEnumerable<HostIntegrityApproval> Merge(IReadOnlyList<AuditLog> approvals, IReadOnlyList<AuditLog> requests)
    {
        var keys = approvals.Select(row => row.ResourceId!).Concat(requests.Select(row => row.ResourceId!)).Distinct(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var approved = approvals.Where(row => row.ResourceId == key).OrderBy(row => row.Timestamp).FirstOrDefault();
            var request = requests.Where(row => row.ResourceId == key).OrderByDescending(row => row.Timestamp).FirstOrDefault();
            var first = request ?? approved!;
            var details = Read(first.Details);
            var parts = key.Split(':');
            yield return new HostIntegrityApproval(key,
                parts.Length == 3 ? parts[1] : details.Host ?? "", parts.Length == 3 ? parts[2] : details.Fingerprint ?? "",
                approved is not null ? "approved" : "pending_second_approver",
                details.Severity ?? HostSecuritySeverities.Warning, details.Reason ?? "",
                first.UserName ?? first.UserId, Utc(first.Timestamp),
                approved is null ? null : approved.UserName ?? approved.UserId, approved is null ? null : Utc(approved.Timestamp));
        }
    }

    private static HostIntegrityApprovalStep Step(AuditLog row) =>
        new(Guid.TryParse(row.UserId, out var id) ? id : Guid.Empty, row.UserName ?? row.UserId, Utc(row.Timestamp), Read(row.Details).Reason ?? "");

    private static Details Read(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new Details(null, null, null, null, null);
        try
        {
            return JsonSerializer.Deserialize<Details>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new Details(null, null, null, null, null);
        }
        catch (JsonException)
        {
            return new Details(null, null, null, null, null);
        }
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

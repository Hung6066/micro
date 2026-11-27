using System.Text.Json;
using His.Hope.IdentityService.Application.HostSecurity;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.HostSecurity;

/// <summary>
/// Acknowledgements live in audit_logs (Action HOST_SECURITY_ACK, ResourceType HostSecurityEvent,
/// ResourceId = the event's ack key): append-only and covered by the audit hash chain, so an
/// acknowledgement can never be silently removed or back-dated.
/// </summary>
public sealed class AuditLogHostSecurityAckStore(IdentityDbContext db) : IHostSecurityAckStore
{
    public const string Action = "HOST_SECURITY_ACK";
    public const string ResourceType = "HostSecurityEvent";

    public async Task<IReadOnlyDictionary<string, HostSecurityAck>> GetAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        if (keys.Count == 0) return new Dictionary<string, HostSecurityAck>();
        var rows = await db.AuditLogs.AsNoTracking()
            // An approved drift (HostIntegrityApprovalStore) counts as acknowledged too.
            .Where(row => ((row.ResourceType == ResourceType && row.Action == Action) ||
                           (row.ResourceType == HostIntegrityApprovalStore.ResourceType && row.Action == HostIntegrityApprovalStore.Approved))
                          && row.ResourceId != null && keys.Contains(row.ResourceId))
            .OrderBy(row => row.Timestamp)
            .Select(row => new { row.ResourceId, row.UserName, row.UserId, row.Timestamp, row.Details, row.Action })
            .ToListAsync(cancellationToken);
        var result = new Dictionary<string, HostSecurityAck>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (result.ContainsKey(row.ResourceId!)) continue; // the first acknowledgement counts
            result[row.ResourceId!] = new HostSecurityAck(row.ResourceId!, row.UserName ?? row.UserId,
                new DateTimeOffset(DateTime.SpecifyKind(row.Timestamp, DateTimeKind.Utc)),
                row.Action == HostIntegrityApprovalStore.Approved ? "approved: " + ReasonOf(row.Details) : NoteOf(row.Details));
        }
        return result;
    }

    public static AuditLog Create(string key, string userId, string? userName, string? note, string severity, string host,
        string securityEvent, string? ip, string? userAgent, string correlationId) => new()
    {
        UserId = userId,
        UserName = userName,
        Action = Action,
        ResourceType = ResourceType,
        ResourceId = key,
        Details = JsonSerializer.Serialize(new { note, severity, host, securityEvent }),
        CorrelationId = correlationId,
        Outcome = "success",
        Source = "host-security",
        IpAddress = ip,
        UserAgent = userAgent is { Length: > 500 } ? userAgent[..500] : userAgent,
    };

    private static string? ReasonOf(string? details)
    {
        if (string.IsNullOrEmpty(details)) return null;
        try
        {
            using var document = JsonDocument.Parse(details);
            return document.RootElement.TryGetProperty("Reason", out var reason) && reason.ValueKind == JsonValueKind.String ? reason.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? NoteOf(string? details)
    {
        if (string.IsNullOrEmpty(details)) return null;
        try
        {
            using var document = JsonDocument.Parse(details);
            return document.RootElement.TryGetProperty("note", out var note) && note.ValueKind == JsonValueKind.String ? note.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

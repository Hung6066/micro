using System.Text.Json;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>
/// The one place that stages a <see cref="SecurityEvent"/> together with its
/// <see cref="SecuritySignalOutbox"/> row, so every security event (API login,
/// browser sign-in risk events) reaches the same audit table and signal path.
/// The caller saves the context.
/// </summary>
public static class SecurityEventWriter
{
    public static SecurityEvent Stage(
        IdentityDbContext context,
        Guid? userId,
        string? userName,
        string eventType,
        string? severity,
        string? ipAddress,
        string? userAgent,
        string? deviceInfo,
        string? details,
        int? riskScore = null,
        string? riskBand = null)
    {
        var now = DateTime.UtcNow;
        var securityEvent = new SecurityEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = Clip(userName, 256),
            EventType = eventType,
            Severity = severity ?? "info",
            IpAddress = Clip(ipAddress, 50),
            UserAgent = Clip(userAgent, 500),
            DeviceInfo = Clip(deviceInfo, 500),
            Details = details,
            Timestamp = now
        };
        context.SecurityEvents.Add(securityEvent);

        var payload = new Dictionary<string, object?>
        {
            ["eventType"] = eventType,
            ["subject"] = userId?.ToString() ?? "unknown",
            ["severity"] = severity ?? "info",
            ["occurredAt"] = now,
        };
        if (riskScore is not null) payload["riskScore"] = riskScore;
        if (riskBand is not null) payload["riskBand"] = riskBand;
        context.SecuritySignalOutbox.Add(new SecuritySignalOutbox
        {
            EventType = eventType,
            Subject = userId?.ToString() ?? userName ?? "unknown",
            PayloadJson = JsonSerializer.Serialize(payload)
        });
        return securityEvent;
    }

    private static string? Clip(string? value, int max) =>
        value is { Length: var length } && length > max ? value[..max] : value;
}

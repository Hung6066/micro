using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace His.Hope.Bff.Core.Audit;

public sealed class BffAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BffAuditMiddleware> _logger;

    // PHI-sensitive URL patterns that trigger mandatory audit
    private static readonly string[] PhiPathPatterns = new[]
    {
        "/api/v1/patients/",
        "/api/v1/encounters/",
        "/api/v1/lab-orders/",
        "/api/v1/prescriptions/",
        "/api/v1/medications/"
    };

    public BffAuditMiddleware(RequestDelegate next, ILogger<BffAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

        await _next(context);
        sw.Stop();

        var path = context.Request.Path.Value ?? "";
        var isPhiAccess = PhiPathPatterns.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        var sessionId = context.Items["SessionId"] as string;
        var userId = context.Items["UserId"] as string; // set by SessionAuthMiddleware
        var permissions = context.Items["Permissions"] as string[];

        // REDACT: never log full patient IDs in non-PHI-context
        var redactedPath = isPhiAccess ? RedactPatientId(path) : path;

        var auditEntry = new BffAuditEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            SessionId = sessionId ?? "anonymous",
            UserId = userId ?? "anonymous",
            Method = context.Request.Method,
            Path = redactedPath,
            StatusCode = context.Response.StatusCode,
            DurationMs = sw.ElapsedMilliseconds,
            ClientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            IsPhiAccess = isPhiAccess,
            Permissions = permissions ?? Array.Empty<string>(),
        };

        // Structured audit log — picked up by ELK for HIPAA compliance
        _logger.LogInformation("BFF_AUDIT {AuditData}", JsonSerializer.Serialize(auditEntry));

        // For PHI access, also log to dedicated HIPAA audit channel
        if (isPhiAccess)
        {
            _logger.LogWarning("BFF_PHI_ACCESS User={UserId} Session={SessionId} Path={Path}",
                userId, sessionId, redactedPath);
        }
    }

    private static string RedactPatientId(string path)
    {
        // /api/v1/patients/PAT-123/timeline → /api/v1/patients/{redacted}/timeline
        var parts = path.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("PAT-") || parts[i].StartsWith("ENC-") ||
                parts[i].StartsWith("LAB-") || parts[i].StartsWith("INV-") ||
                parts[i].StartsWith("MED-") || parts[i].StartsWith("PRX-"))
            {
                parts[i] = "{redacted}";
            }
        }
        return string.Join('/', parts);
    }
}

public sealed record BffAuditEntry
{
    public DateTimeOffset Timestamp { get; init; }
    public string CorrelationId { get; init; } = "";
    public string SessionId { get; init; } = "";
    public string UserId { get; init; } = "";
    public string Method { get; init; } = "";
    public string Path { get; init; } = "";
    public int StatusCode { get; init; }
    public long DurationMs { get; init; }
    public string ClientIp { get; init; } = "";
    public string UserAgent { get; init; } = "";
    public bool IsPhiAccess { get; init; }
    public string[] Permissions { get; init; } = Array.Empty<string>();
}

public static class BffAuditMiddlewareExtensions
{
    public static IApplicationBuilder UseBffAudit(this IApplicationBuilder builder)
        => builder.UseMiddleware<BffAuditMiddleware>();
}

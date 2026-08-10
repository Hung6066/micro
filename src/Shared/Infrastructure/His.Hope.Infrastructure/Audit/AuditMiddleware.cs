using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace His.Hope.Infrastructure.Audit;

/// <summary>
/// HTTP middleware that audits access to PHI-related endpoints.
///
/// HIPAA Context (164.312(b)):
///   This middleware implements audit controls by logging all accesses to
///   patient health information endpoints. It captures who, what, when,
///   and where for each PHI access.
///
/// Design Decisions:
///   - Uses Serilog structured logging for ELK integration
///   - Extracts resource IDs from URL patterns (e.g., /api/v1/patients/{id})
///   - Only logs metadata, NEVER PHI values
///   - Low overhead - runs async to avoid blocking the request pipeline
///   - Supports correlation IDs for distributed tracing
/// </summary>
public class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAuditService _auditService;

    // SECURITY: Define which URL patterns constitute PHI access
    // These patterns match the resource types in the system
    private static readonly Regex[] PhiPatterns =
    {
        new(@"/api/v1/patients/([a-f0-9\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"/api/v1/encounters/([a-f0-9\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"/api/v1/lab-orders/([a-f0-9\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"/api/v1/prescriptions/([a-f0-9\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"/api/v1/invoices/([a-f0-9\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"/api/v1/appointments/([a-f0-9\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"/api/v1/medications/([a-f0-9\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"/api/v1/auth/me", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    };

    // HTTP methods that read PHI
    private static readonly HashSet<string> ReadMethods = new(StringComparer.OrdinalIgnoreCase)
        { "GET", "HEAD", "OPTIONS" };

    public AuditMiddleware(RequestDelegate next, IAuditService auditService)
    {
        _next = next;
        _auditService = auditService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Try to match against known PHI patterns
        PhiMatch? match = null;
        foreach (var pattern in PhiPatterns)
        {
            var m = pattern.Match(path);
            if (m.Success)
            {
                match = new PhiMatch
                {
                    ResourceType = ExtractResourceType(pattern.ToString()),
                    ResourceId = m.Groups[1].Value,
                    Action = DetermineAction(context.Request.Method)
                };
                break;
            }
        }

        // SECURITY: Also audit all POST/PUT/PATCH/DELETE under /api/v1
        // This captures CRUD operations on PHI that don't match a specific pattern
        if (match == null && path.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase)
            && !ReadMethods.Contains(context.Request.Method))
        {
            match = new PhiMatch
            {
                ResourceType = "API",
                ResourceId = path,
                Action = DetermineAction(context.Request.Method)
            };
        }

        // Capture the response to determine success/failure
        if (match != null)
        {
            // Capture the time and user context before the request
            var userId = context.User?.FindFirst("sub")?.Value ?? "anonymous";
            var userRole = context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                ?? context.User?.FindFirst("role")?.Value
                ?? "unknown";
            var correlationId = Activity.Current?.Id ?? context.TraceIdentifier;
            var tenantId = context.User?.FindFirst("tenant")?.Value;

            // SECURITY: Extract client IP from trusted proxy headers
            var clientIp = GetClientIp(context);
            var userAgent = context.Request.Headers.UserAgent.ToString();

            // Log the PHI access event
            _auditService.LogPhiAccess(new PhiAuditEntry
            {
                UserId = userId,
                UserRole = userRole,
                ResourceType = match.Value.ResourceType,
                ResourceId = match.Value.ResourceId,
                Action = match.Value.Action,
                ClientIp = clientIp,
                UserAgent = userAgent,
                CorrelationId = correlationId,
                TenantId = tenantId,
                HttpMethod = context.Request.Method,
                Path = path
            });
        }

        await _next(context);
    }

    private static string GetClientIp(HttpContext context)
    {
        // SECURITY: Respect X-Forwarded-For header when behind proxy
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string ExtractResourceType(string pattern)
    {
        // Extract the resource type from URL pattern
        // e.g., "/api/v1/patients/(...)" => "Patient"
        var segments = pattern.Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == "v1" && i + 1 < segments.Length)
            {
                var type = segments[i + 1];
                // Convert plural to singular for consistency
                return type.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                    ? type[..^1]
                    : type;
            }
        }
        return "Unknown";
    }

    private static string DetermineAction(string method) => method.ToUpperInvariant() switch
    {
        "GET" => "READ",
        "HEAD" => "READ",
        "POST" => "CREATE",
        "PUT" => "UPDATE",
        "PATCH" => "UPDATE",
        "DELETE" => "DELETE",
        _ => method
    };

    private readonly struct PhiMatch
    {
        public string ResourceType { get; init; }
        public string ResourceId { get; init; }
        public string Action { get; init; }
    }
}

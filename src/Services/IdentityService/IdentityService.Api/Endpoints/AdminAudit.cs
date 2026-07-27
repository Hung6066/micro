using His.Hope.Infrastructure.Audit;

namespace His.Hope.IdentityService.Api.Endpoints;

internal static class AdminAudit
{
    public static Task LogAsync(IAuditService audit, HttpContext http, string action, string resource, string? resourceId, CancellationToken ct) =>
        audit.LogPhiAccessAsync(new PhiAuditEntry
        {
            UserId = http.User.FindFirst("sub")?.Value ?? "system",
            UserRole = http.User.FindFirst("role")?.Value,
            ResourceType = resource,
            ResourceId = resourceId,
            Action = action,
            ClientIp = http.Connection.RemoteIpAddress?.ToString(),
            UserAgent = http.Request.Headers.UserAgent.ToString(),
            CorrelationId = http.Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? http.TraceIdentifier,
            HttpMethod = http.Request.Method,
            Path = http.Request.Path
        }, ct);
}

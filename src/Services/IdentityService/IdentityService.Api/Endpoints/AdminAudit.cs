using His.Hope.Infrastructure.Audit;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using System.Text.Json;
using His.Hope.SharedKernel.Authorization;

namespace His.Hope.IdentityService.Api.Endpoints;

internal static class AdminAudit
{
    // Column limits of the audit table. An oversized value used to fail the
    // insert after the change had already been committed, losing the record.
    private static string? Truncate(string? value, int maxLength) =>
        value is { Length: var length } && length > maxLength ? value[..maxLength] : value;

    public static Task LogAsync(IAuditService audit, HttpContext http, string action, string resource, string? resourceId, CancellationToken ct) =>
        audit.LogPhiAccessAsync(new PhiAuditEntry
        {
            UserId = http.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value ?? "system",
            UserRole = http.User.FindFirst("role")?.Value,
            ResourceType = resource,
            ResourceId = resourceId ?? string.Empty,
            Action = action,
            ClientIp = http.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Truncate(http.Request.Headers.UserAgent.ToString(), 500),
            CorrelationId = http.Response.Headers[His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Headers.CorrelationId].FirstOrDefault() ?? http.TraceIdentifier,
            HttpMethod = http.Request.Method,
            Path = http.Request.Path
        }, ct);

    public static async Task LogAuthorizationChangeAsync(
        IApplicationDbContext db,
        HttpContext http,
        string action,
        string resourceType,
        string resourceId,
        string reason,
        string? beforeJson,
        string? afterJson,
        CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = http.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value ?? "system",
            UserName = http.User.Identity?.Name,
            Action = $"AUTHZ_{action.ToUpperInvariant()}",
            ResourceType = resourceType,
            ResourceId = resourceId,
            Details = JsonSerializer.Serialize(new
            {
                reason,
                audience = "identity-control-plane",
                principalType = http.User.FindFirst(AuthorizationConstants.Claims.PrincipalType)?.Value ?? AuthorizationConstants.PrincipalTypes.Human
            }),
            BeforeJson = Truncate(beforeJson, 8000),
            AfterJson = Truncate(afterJson, 8000),
            CorrelationId = http.TraceIdentifier,
            Outcome = "success",
            Source = "authorization-control-plane",
            IpAddress = http.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Truncate(http.Request.Headers.UserAgent.ToString(), 500)
        });
        await db.SaveChangesAsync(ct);
    }
}

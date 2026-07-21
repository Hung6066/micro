using His.Hope.IdentityService.Application.UseCases.AuditLogs.Queries;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using MediatR;
using System.Security.Claims;
using System.Text.Json;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// Audit log query endpoints for HIPAA compliance reporting.
/// All endpoints require authorization.
/// </summary>
public static class AuditLogEndpoints
{
    public static RouteGroupBuilder MapAuditLogEndpoints(this RouteGroupBuilder group)
    {
        // POST /api/v1/audit/events - Client-side audit event ingestion
        group.MapPost("/audit/events", async (
            AuditEventsRequest request,
            HttpContext httpContext,
            IdentityDbContext db,
            CancellationToken ct) =>
        {
            if (request.Events is null || request.Events.Count == 0)
                return Results.Accepted(value: new { accepted = 0 });

            if (request.Events.Count > 100)
                return Results.BadRequest(new { error = "Audit batch exceeds the 100 event limit" });

            var authenticatedUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.User.FindFirst("sub")?.Value
                ?? string.Empty;
            var userName = httpContext.User.Identity?.Name
                ?? httpContext.User.FindFirst(ClaimTypes.Name)?.Value;
            var ipAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                ?? httpContext.Connection.RemoteIpAddress?.ToString();

            foreach (var auditEvent in request.Events)
            {
                var userId = string.IsNullOrWhiteSpace(auditEvent.UserId)
                    ? authenticatedUserId
                    : auditEvent.UserId.Trim();

                db.AuditLogs.Add(new AuditLog
                {
                    UserId = Truncate(userId, 100),
                    UserName = Truncate(userName, 200),
                    Action = Truncate(auditEvent.Action, 50),
                    ResourceType = "ClientAudit",
                    ResourceId = Truncate(ReadDetailString(auditEvent.Details, "resourceId")
                        ?? ReadDetailString(auditEvent.Details, "patientId"), 100),
                    Details = Truncate(SerializeDetails(auditEvent), 2000),
                    IpAddress = Truncate(ipAddress, 50),
                    UserAgent = Truncate(auditEvent.UserAgent
                        ?? httpContext.Request.Headers.UserAgent.ToString(), 500),
                    Timestamp = FromUnixMilliseconds(auditEvent.Timestamp)
                });
            }

            await db.SaveChangesAsync(ct);
            return Results.Accepted(value: new { accepted = request.Events.Count });
        }).RequireAuthorization();

        // GET /api/v1/audit-logs - Paginated audit log search
        group.MapGet("/audit-logs", async (
            int page = 1,
            int pageSize = 20,
            string? userId = null,
            string? action = null,
            string? resourceType = null,
            string? resourceId = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            var result = await mediator.Send(
                new GetAuditLogsQuery(page, pageSize, userId, action,
                    resourceType, resourceId, dateFrom, dateTo), ct);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:admin.audit.read");

        // GET /api/v1/audit-logs/{id} - Audit log detail
        group.MapGet("/audit-logs/{id:guid}", async (
            Guid id,
            IMediator mediator = null!,
            CancellationToken ct = default) =>
        {
            var log = await mediator.Send(new GetAuditLogByIdQuery(id), ct);
            return log is null ? Results.NotFound() : Results.Ok(log);
        }).RequireAuthorization("Permission:admin.audit.read");

        return group;
    }

    private static DateTime FromUnixMilliseconds(long timestamp)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTime.UtcNow;
        }
    }

    private static string SerializeDetails(ClientAuditEvent auditEvent)
    {
        return JsonSerializer.Serialize(new
        {
            auditEvent.Details,
            auditEvent.CorrelationId
        });
    }

    private static string? ReadDetailString(JsonElement? details, string propertyName)
    {
        if (details is null || details.Value.ValueKind != JsonValueKind.Object)
            return null;

        return details.Value.TryGetProperty(propertyName, out var property)
            ? property.ToString()
            : null;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public sealed record AuditEventsRequest(List<ClientAuditEvent>? Events);

public sealed record ClientAuditEvent(
    string Action,
    long Timestamp,
    string? UserId,
    JsonElement? Details,
    string? CorrelationId,
    string? UserAgent);

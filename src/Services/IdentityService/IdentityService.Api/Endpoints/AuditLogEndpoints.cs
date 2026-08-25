using His.Hope.IdentityService.Application.UseCases.AuditLogs.Queries;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.Authorization;
using His.Hope.IdentityService.Api.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using His.Hope.SharedKernel.Authorization;
using System.Text;
using System.Text.Json;
using His.Hope.Contracts.Query;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// Audit log query endpoints for HIPAA compliance reporting.
/// All endpoints require authorization.
/// </summary>
public static class AuditLogEndpoints
{
    private const int MaxAuditEventsPerRequest = 100;

    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "view", "create", "update", "delete", "search", "export", "print",
        "login", "logout", "access", "modify",
        // Shared frontend audit protocol uses resource-scoped action names.
        // Keep the legacy names above for older clients during migration.
        "auth.login", "auth.logout", "auth.refresh",
        "data.view", "data.create", "data.update", "data.delete",
        "error.client", "error.server", "security.csp-violation",
        "navigation.change"
        ,"read_patient"
    };

    private static readonly HashSet<string> SessionAuditActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "login", "logout", "auth.login", "auth.logout", "auth.refresh"
    };

    public static RouteGroupBuilder MapAuditLogEndpoints(this RouteGroupBuilder group)
    {
        // POST /api/v1/audit/events - Client-side audit event ingestion
        group.MapPost("/audit/events", async (
            AuditEventsRequest request,
            HttpContext httpContext,
            IdentityDbContext db,
            IAuthorizationService authorization,
            CancellationToken ct) =>
        {
            if (request.Events is null || request.Events.Count == 0)
                return Results.Accepted(value: new AuditEventsResponse(0, 0));

            var invalidActions = request.Events
                .Take(MaxAuditEventsPerRequest)
                .Where(e => !AllowedActions.Contains(e.Action ?? ""))
                .Select(e => e.Action)
                .Distinct()
                .ToList();

            if (invalidActions.Count > 0)
            {
                return Results.Problem(
                    $"Invalid audit action(s): {string.Join(", ", invalidActions)}. Allowed: {string.Join(", ", AllowedActions)}",
                    statusCode: 400);
            }

            var acceptedEvents = request.Events.Take(MaxAuditEventsPerRequest).ToList();
            var denied = await ValidateAuditAuthorizationAsync(httpContext.User, authorization, acceptedEvents);
            if (denied is not null) return denied;

            var authenticatedUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext.User.FindFirst("sub")?.Value
                ?? string.Empty;
            var userName = httpContext.User.Identity?.Name
                ?? httpContext.User.FindFirst(ClaimTypes.Name)?.Value;
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            var serverTimestamp = DateTime.UtcNow;

            foreach (var auditEvent in acceptedEvents)
            {
                var correlationId = auditEvent.CorrelationId;
                if (!string.IsNullOrWhiteSpace(correlationId) && !Guid.TryParse(correlationId, out _))
                    correlationId = null;

                var eventForSerialization = correlationId != auditEvent.CorrelationId
                    ? auditEvent with { CorrelationId = correlationId }
                    : auditEvent;

                var serializedDetails = SerializeDetails(eventForSerialization);
                if (Encoding.UTF8.GetByteCount(serializedDetails) > 8192)
                    serializedDetails = TruncateUtf8(serializedDetails, 8192);

                db.AuditLogs.Add(new AuditLog
                {
                    UserId = Truncate(authenticatedUserId, 100),
                    UserName = Truncate(userName, 200),
                    Action = Truncate(auditEvent.Action, 50),
                    ResourceType = "ClientAudit",
                    ResourceId = Truncate(ReadDetailString(auditEvent.Details, "resourceId")
                        ?? ReadDetailString(auditEvent.Details, "patientId"), 100),
                    Details = Truncate(serializedDetails, 2000),
                    IpAddress = Truncate(ipAddress, 50),
                    UserAgent = Truncate(userAgent, 500),
                    CorrelationId = correlationId,
                    Outcome = "accepted",
                    Source = "client-audit",
                    Timestamp = serverTimestamp
                });
            }

            await db.SaveChangesAsync(ct);
            return Results.Accepted(value: new AuditEventsResponse(
                acceptedEvents.Count,
                Math.Max(0, request.Events.Count - acceptedEvents.Count)));
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
            string? sort = null,
            [FromServices] IMediator mediator = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            try { new QueryRequest(page, pageSize, Sort: sort).Validate(); SortContract.Parse(sort, new HashSet<string>(["action", "resourcetype", "timestamp"], StringComparer.OrdinalIgnoreCase)); }
            catch (ArgumentException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["query"] = [ex.Message] }); }
            if (new[] { userId, action, resourceType, resourceId }.Any(value => value?.Length > 100))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["filter"] = ["Audit filters must be 100 characters or fewer."] });

            var tenantFilter = IamTenantHttpContext.RequireFilter(http);

            var result = await mediator.Send(
                new GetAuditLogsQuery(page, pageSize, userId, action,
                    resourceType, resourceId, dateFrom, dateTo, sort,
                    tenantFilter.AllowedTenantKeys?.ToArray()), ct);
            return Results.Ok(result);
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminAuditRead)
            .WithTenantReadScope(HisHopePermissions.Admin.AuditRead);

        // GET /api/v1/audit-logs/{id} - Audit log detail
        group.MapGet("/audit-logs/{id:guid}", async (
            Guid id,
            [FromServices] IMediator mediator = null!,
            IdentityDbContext db = null!,
            HttpContext http = null!,
            CancellationToken ct = default) =>
        {
            var tenantFilter = IamTenantHttpContext.RequireFilter(http);

            if (tenantFilter.AllowedTenantKeys is not null &&
                !await db.AuditLogs.AsNoTracking()
                    .Where(item => item.Id == id)
                    .WhereTenantActor(db, tenantFilter.AllowedTenantKeys)
                    .AnyAsync(ct))
                return Results.NotFound();

            var log = await mediator.Send(new GetAuditLogByIdQuery(id), ct);
            return log is null ? Results.NotFound() : Results.Ok(log);
        })
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminAuditRead)
            .WithTenantReadScope(HisHopePermissions.Admin.AuditRead);

        return group;
    }

    private static string SerializeDetails(ClientAuditEvent auditEvent)
    {
        var details = auditEvent.Details is { } value ? Redact(value) : (JsonElement?)null;
        return JsonSerializer.Serialize(new
        {
            Details = details,
            auditEvent.CorrelationId
        });
    }

    private static JsonElement Redact(JsonElement value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(RedactValue(value)));
        return document.RootElement.Clone();
    }

    private static object? RedactValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Object => value.EnumerateObject().ToDictionary(
                property => property.Name,
                property => IsSensitive(property.Name) ? (object?)"[REDACTED]" : RedactValue(property.Value),
                StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => value.EnumerateArray().Select(RedactValue).ToArray(),
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static bool IsSensitive(string name) =>
        name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("privatekey", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("clientcertificate", StringComparison.OrdinalIgnoreCase);

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

    private static string TruncateUtf8(string value, int maxBytes)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length <= maxBytes) return value;
        var truncated = Encoding.UTF8.GetString(bytes, 0, maxBytes);
        var lastFullChar = truncated.Length;
        while (lastFullChar > 0 && char.IsHighSurrogate(truncated[lastFullChar - 1]))
            lastFullChar--;
        return truncated[..lastFullChar] + "...[truncated]";
    }

    private static async Task<IResult?> ValidateAuditAuthorizationAsync(
        ClaimsPrincipal user,
        IAuthorizationService authorization,
        IReadOnlyCollection<ClientAuditEvent> events)
    {
        foreach (var action in events.Select(item => item.Action).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (SessionAuditActions.Contains(action))
                continue;

            var policy = action.Equals("read_patient", StringComparison.OrdinalIgnoreCase)
                ? AuthorizationPolicyNames.Permissions.PatientsView
                : AuthorizationPolicyNames.Permissions.AdminAuditRead;

            if (!(await authorization.AuthorizeAsync(user, null, policy)).Succeeded)
                return Results.Forbid();
        }

        return null;
    }
}

public sealed record AuditEventsRequest(List<ClientAuditEvent>? Events);

public sealed record AuditEventsResponse(int Accepted, int Dropped);

public sealed record ClientAuditEvent(
    string Action,
    long Timestamp,       // IGNORED — server uses UtcNow
    string? UserId,       // IGNORED — server extracts from JWT
    JsonElement? Details,
    string? CorrelationId,
    string? UserAgent);   // IGNORED — server extracts from request header

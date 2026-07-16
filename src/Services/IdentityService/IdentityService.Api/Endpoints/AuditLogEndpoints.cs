using His.Hope.IdentityService.Application.UseCases.AuditLogs.Queries;
using MediatR;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// Audit log query endpoints for HIPAA compliance reporting.
/// All endpoints require authorization.
/// </summary>
public static class AuditLogEndpoints
{
    public static RouteGroupBuilder MapAuditLogEndpoints(this RouteGroupBuilder group)
    {
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
}

using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.AuditLogs.Queries;

public record GetAuditLogsQuery(
    int Page = 1,
    int PageSize = 20,
    string? UserId = null,
    string? Action = null,
    string? ResourceType = null,
    string? ResourceId = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null)
    : IRequest<PagedResult<AuditLogDto>>;

public class GetAuditLogsQueryHandler
    : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAuditLogsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<AuditLogDto>> Handle(GetAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        IQueryable<AuditLog> query = _context.AuditLogs;

        // Apply filters
        if (!string.IsNullOrWhiteSpace(request.UserId))
            query = query.Where(al => al.UserId == request.UserId);

        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(al => al.Action == request.Action);

        if (!string.IsNullOrWhiteSpace(request.ResourceType))
            query = query.Where(al => al.ResourceType == request.ResourceType);

        if (!string.IsNullOrWhiteSpace(request.ResourceId))
            query = query.Where(al => al.ResourceId == request.ResourceId);

        if (request.DateFrom.HasValue)
            query = query.Where(al => al.Timestamp >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(al => al.Timestamp <= request.DateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(al => al.Timestamp)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(al => new AuditLogDto(
                al.Id, al.UserId, al.UserName, al.Action,
                al.ResourceType, al.ResourceId, al.Details,
                al.IpAddress, al.UserAgent, al.Timestamp))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>(items, totalCount, request.Page, request.PageSize);
    }
}

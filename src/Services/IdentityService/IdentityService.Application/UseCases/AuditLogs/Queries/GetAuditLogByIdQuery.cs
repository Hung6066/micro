using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.AuditLogs.Queries;

public record GetAuditLogByIdQuery(Guid Id) : IRequest<AuditLogDto?>;

public class GetAuditLogByIdQueryHandler
    : IRequestHandler<GetAuditLogByIdQuery, AuditLogDto?>
{
    private readonly IApplicationDbContext _context;

    public GetAuditLogByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AuditLogDto?> Handle(GetAuditLogByIdQuery request,
        CancellationToken cancellationToken)
    {
        var log = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.Id == request.Id, cancellationToken);

        if (log is null) return null;

        return new AuditLogDto(
            log.Id, log.UserId, log.UserName, log.Action,
            log.ResourceType, log.ResourceId, log.Details,
            log.IpAddress, log.UserAgent, log.Timestamp);
    }
}

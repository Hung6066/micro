using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Roles.Queries;

public record GetPermissionsQuery : IRequest<List<PermissionDto>>;

public class GetPermissionsQueryHandler : IRequestHandler<GetPermissionsQuery, List<PermissionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPermissionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PermissionDto>> Handle(GetPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Permissions
            .OrderBy(p => p.Group)
            .ThenBy(p => p.Code)
            .Select(p => new PermissionDto(
                p.Code, p.Name, p.Group, p.Description, p.IsSystem))
            .ToListAsync(cancellationToken);
    }
}

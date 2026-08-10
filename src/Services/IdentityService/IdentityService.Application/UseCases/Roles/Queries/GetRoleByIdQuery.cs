using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Roles.Queries;

public record GetRoleByIdQuery(Guid Id) : IRequest<RoleDto?>;

public class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, RoleDto?>
{
    private readonly IApplicationDbContext _context;

    public GetRoleByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RoleDto?> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (role is null) return null;

        return new RoleDto(
            role.Id,
            role.Name!,
            role.Description,
            role.IsSystem,
            role.CreatedAt,
            role.RolePermissions.Select(rp => new PermissionDto(
                rp.PermissionCode,
                rp.Permission.Name,
                rp.Permission.Group,
                rp.Permission.Description,
                rp.Permission.IsSystem
            )).ToList()
        );
    }
}

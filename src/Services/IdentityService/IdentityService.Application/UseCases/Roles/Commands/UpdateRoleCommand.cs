using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Roles.Commands;

public record UpdateRoleCommand(
    Guid Id,
    string Name,
    string? Description,
    string[]? Permissions)
    : IRequest<RoleDto>;

public class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, RoleDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateRoleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RoleDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Role not found.");

        // Update role properties
        role.Name = request.Name;
        role.NormalizedName = request.Name.ToUpperInvariant();
        role.Description = request.Description;

        // Update permissions: replace all existing with new set
        _context.RolePermissions.RemoveRange(role.RolePermissions);

        if (request.Permissions is { Length: > 0 })
        {
            foreach (var permissionCode in request.Permissions)
            {
                var permission = await _context.Permissions
                    .FirstOrDefaultAsync(p => p.Code == permissionCode, cancellationToken);

                if (permission is not null)
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionCode = permissionCode
                    });
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Reload with permissions
        var updated = await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.Id == role.Id, cancellationToken);

        return new RoleDto(
            updated.Id,
            updated.Name!,
            updated.Description,
            updated.IsSystem,
            updated.CreatedAt,
            updated.RolePermissions.Select(rp => new PermissionDto(
                rp.PermissionCode,
                rp.Permission.Name,
                rp.Permission.Group,
                rp.Permission.Description,
                rp.Permission.IsSystem
            )).ToList()
        );
    }
}

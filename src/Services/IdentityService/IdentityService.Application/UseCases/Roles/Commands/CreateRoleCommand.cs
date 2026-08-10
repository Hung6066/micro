using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Roles.Commands;

public record CreateRoleCommand(
    string Name,
    string? Description,
    string[]? Permissions)
    : IRequest<RoleDto>;

public class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, RoleDto>
{
    private readonly IApplicationDbContext _context;

    public CreateRoleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RoleDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        // Check for duplicate role name
        var exists = await _context.Roles.AnyAsync(
            r => r.NormalizedName == request.Name.ToUpper(), cancellationToken);
        if (exists)
            throw new InvalidOperationException($"Role '{request.Name}' already exists.");

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            NormalizedName = request.Name.ToUpperInvariant(),
            Description = request.Description,
            IsSystem = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Roles.Add(role);

        // Assign permissions if specified
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
        var savedRole = await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.Id == role.Id, cancellationToken);

        return new RoleDto(
            savedRole.Id,
            savedRole.Name!,
            savedRole.Description,
            savedRole.IsSystem,
            savedRole.CreatedAt,
            savedRole.RolePermissions.Select(rp => new PermissionDto(
                rp.PermissionCode,
                rp.Permission.Name,
                rp.Permission.Group,
                rp.Permission.Description,
                rp.Permission.IsSystem
            )).ToList()
        );
    }
}

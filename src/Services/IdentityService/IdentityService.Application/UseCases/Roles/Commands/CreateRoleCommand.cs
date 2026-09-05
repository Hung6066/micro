using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Application.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.IdentityService.Application.UseCases.Roles.Commands;

public record CreateRoleCommand(
    string Name,
    string? Description,
    string[]? Permissions,
    string? Owner)
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
            Guard.Against.Conflict(true, $"Role '{request.Name}' already exists.");

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            NormalizedName = request.Name.ToUpperInvariant(),
            Description = request.Description,
            Owner = string.IsNullOrWhiteSpace(request.Owner) ? "identity-service" : request.Owner.Trim(),
            IsSystem = false,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };

        _context.Roles.Add(role);

        // Permission references are strict: silently dropping an unknown code
        // would create a role whose effective access differs from the admin UI.
        var permissionCodes = RoleGovernanceRules.NormalizePermissionCodes(request.Permissions);
        var knownPermissions = await _context.Permissions
            .Where(permission => permissionCodes.Contains(permission.Code))
            .Select(permission => permission.Code)
            .ToListAsync(cancellationToken);
        var unknownPermission = permissionCodes.FirstOrDefault(code =>
            !knownPermissions.Contains(code, StringComparer.OrdinalIgnoreCase));
        if (unknownPermission is not null)
            throw new InvalidOperationException($"Unknown permission '{unknownPermission}'.");

        if (permissionCodes.Length > 0)
        {
            foreach (var permissionCode in permissionCodes)
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionCode = knownPermissions.First(code =>
                        string.Equals(code, permissionCode, StringComparison.OrdinalIgnoreCase))
                });
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
            )).ToList(),
            savedRole.ConcurrencyStamp,
            savedRole.Owner,
            savedRole.AuthorizationVersion,
            savedRole.RiskTier,
            savedRole.ReviewCadenceDays,
            savedRole.LifecycleStatus,
            savedRole.PublishedAt,
            savedRole.PublishedBy
        );
    }
}

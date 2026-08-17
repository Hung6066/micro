using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Application.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Roles.Commands;

public record UpdateRoleCommand(
    Guid Id,
    string Name,
    string? Description,
    string[]? Permissions,
    string? ConcurrencyToken,
    string? Owner)
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

        if (!string.IsNullOrWhiteSpace(request.ConcurrencyToken) &&
            !string.Equals(request.ConcurrencyToken, role.ConcurrencyStamp, StringComparison.Ordinal))
            throw new InvalidOperationException("CONCURRENCY_CONFLICT: The role was changed by another request.");

        if (role.IsSystem)
            throw new InvalidOperationException("System roles are immutable.");

        // Update role properties
        role.Name = request.Name;
        role.NormalizedName = request.Name.ToUpperInvariant();
        role.Description = request.Description;
        if (!string.IsNullOrWhiteSpace(request.Owner))
            role.Owner = request.Owner.Trim();
        role.AuthorizationVersion++;
        role.PublishedAt = DateTime.UtcNow;
        role.PublishedBy = "identity-control-plane";
        role.ConcurrencyStamp = Guid.NewGuid().ToString("N");

        // Permission references are strict and replacement is atomic.
        var permissionCodes = RoleGovernanceRules.NormalizePermissionCodes(request.Permissions);
        var knownPermissions = await _context.Permissions
            .Where(permission => permissionCodes.Contains(permission.Code))
            .Select(permission => permission.Code)
            .ToListAsync(cancellationToken);
        var unknownPermission = permissionCodes.FirstOrDefault(code =>
            !knownPermissions.Contains(code, StringComparer.OrdinalIgnoreCase));
        if (unknownPermission is not null)
            throw new InvalidOperationException($"Unknown permission '{unknownPermission}'.");

        _context.RolePermissions.RemoveRange(role.RolePermissions);

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
            )).ToList(),
            updated.ConcurrencyStamp,
            updated.Owner,
            updated.AuthorizationVersion,
            updated.RiskTier,
            updated.ReviewCadenceDays,
            updated.LifecycleStatus,
            updated.PublishedAt,
            updated.PublishedBy
        );
    }
}

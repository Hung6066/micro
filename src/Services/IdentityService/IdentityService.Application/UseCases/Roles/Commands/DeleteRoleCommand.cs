using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.IdentityService.Application.UseCases.Roles.Commands;

public record DeleteRoleCommand(Guid Id) : IRequest;

public class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteRoleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? Guard.Against.NotFound<Role>(null, "Role", request.Id);

        if (role.IsSystem)
            Guard.Against.Conflict(true, "System roles cannot be deleted.");

        // Check if any users are assigned to this role
        var hasUsers = await _context.UserRoles
            .AnyAsync(ur => ur.RoleId == request.Id, cancellationToken);

        if (hasUsers)
            Guard.Against.Conflict(true,
                "Cannot delete role because it has users assigned. Remove all users from this role first.");

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

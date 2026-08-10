using His.Hope.IdentityService.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
            ?? throw new KeyNotFoundException("Role not found.");

        if (role.IsSystem)
            throw new InvalidOperationException("System roles cannot be deleted.");

        // Check if any users are assigned to this role
        var hasUsers = await _context.UserRoles
            .AnyAsync(ur => ur.RoleId == request.Id, cancellationToken);

        if (hasUsers)
            throw new InvalidOperationException(
                "Cannot delete role because it has users assigned. Remove all users from this role first.");

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

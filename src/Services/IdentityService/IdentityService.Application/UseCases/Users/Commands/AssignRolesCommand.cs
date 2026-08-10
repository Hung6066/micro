using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Users.Commands;

public record AssignRolesCommand(Guid UserId, string[] RoleIds) : IRequest<UserDetailDto>;

public class AssignRolesCommandHandler : IRequestHandler<AssignRolesCommand, UserDetailDto>
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;

    public AssignRolesCommandHandler(UserManager<User> userManager, RoleManager<Role> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<UserDetailDto> Handle(AssignRolesCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        // Resolve role IDs to role names
        var roleNames = new List<string>();
        foreach (var roleId in request.RoleIds)
        {
            if (Guid.TryParse(roleId, out var guidId))
            {
                var role = await _roleManager.FindByIdAsync(roleId);
                if (role is not null)
                    roleNames.Add(role.Name!);
            }
            else
            {
                // If not a GUID, treat as role name directly
                roleNames.Add(roleId);
            }
        }

        // Remove all current roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
            await _userManager.RemoveFromRolesAsync(user, currentRoles);

        // Assign new roles
        if (roleNames.Count > 0)
        {
            var result = await _userManager.AddToRolesAsync(user, roleNames);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Role assignment failed: {errors}");
            }
        }

        var updatedRoles = await _userManager.GetRolesAsync(user);

        return new UserDetailDto(
            user.Id, user.UserName!, user.Email!, user.PhoneNumber,
            user.FirstName, user.LastName, user.MiddleName,
            user.FullName, user.LicenseNumber, user.Specialty,
            user.IsActive, user.CreatedAt, user.LastLoginAt, updatedRoles);
    }
}

using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Users.Commands;

public record UpdateUserCommand(
    Guid Id,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    string? Role,
    bool? IsActive)
    : IRequest<UserDetailDto>;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserDetailDto>
{
    private readonly UserManager<User> _userManager;

    public UpdateUserCommandHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserDetailDto> Handle(UpdateUserCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        // Update properties if provided
        if (request.FirstName is not null)
            user.FirstName = request.FirstName;

        if (request.LastName is not null)
            user.LastName = request.LastName;

        if (request.Email is not null)
        {
            var emailOwner = await _userManager.FindByEmailAsync(request.Email);
            if (emailOwner is not null && emailOwner.Id != user.Id)
                throw new InvalidOperationException("Email already in use by another user.");
            user.Email = request.Email;
            user.UserName = request.Email; // Keep username in sync with email
            user.NormalizedEmail = _userManager.NormalizeEmail(request.Email);
            user.NormalizedUserName = _userManager.NormalizeName(request.Email);
        }

        if (request.PhoneNumber is not null)
            user.PhoneNumber = request.PhoneNumber;

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"User update failed: {errors}");
        }

        // Update role if specified
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Count > 0)
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, request.Role);
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new UserDetailDto(
            user.Id, user.UserName!, user.Email!, user.PhoneNumber,
            user.FirstName, user.LastName, user.MiddleName,
            user.FullName, user.LicenseNumber, user.Specialty,
            user.IsActive, user.CreatedAt, user.LastLoginAt, roles);
    }
}

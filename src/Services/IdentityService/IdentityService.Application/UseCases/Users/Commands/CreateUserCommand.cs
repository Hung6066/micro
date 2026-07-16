using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace His.Hope.IdentityService.Application.UseCases.Users.Commands;

public record CreateUserCommand(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? LicenseNumber,
    string? Specialty,
    string? PhoneNumber,
    string? Role)
    : IRequest<UserDetailDto>;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDetailDto>
{
    private readonly UserManager<User> _userManager;

    public CreateUserCommandHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserDetailDto> Handle(CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        // Check for duplicate username
        var existingUser = await _userManager.FindByNameAsync(request.Username);
        if (existingUser is not null)
            throw new InvalidOperationException("Username already exists.");

        // Check for duplicate email
        var existingEmail = await _userManager.FindByEmailAsync(request.Email);
        if (existingEmail is not null)
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = request.Username,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            LicenseNumber = request.LicenseNumber,
            Specialty = request.Specialty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"User creation failed: {errors}");
        }

        // Assign role (default to "Provider" if not specified)
        var roleName = string.IsNullOrWhiteSpace(request.Role) ? "Provider" : request.Role;
        await _userManager.AddToRoleAsync(user, roleName);

        var roles = await _userManager.GetRolesAsync(user);

        return new UserDetailDto(
            user.Id, user.UserName!, user.Email!, user.PhoneNumber,
            user.FirstName, user.LastName, user.MiddleName,
            user.FullName, user.LicenseNumber, user.Specialty,
            user.IsActive, user.CreatedAt, user.LastLoginAt, roles);
    }
}

using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Users.Queries;

public record GetUserByIdQuery(Guid Id) : IRequest<UserDetailDto?>;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDetailDto?>
{
    private readonly UserManager<User> _userManager;

    public GetUserByIdQueryHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserDetailDto?> Handle(GetUserByIdQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null) return null;

        var roles = await _userManager.GetRolesAsync(user);

        return new UserDetailDto(
            user.Id, user.UserName!, user.Email!, user.PhoneNumber,
            user.FirstName, user.LastName, user.MiddleName,
            user.FullName, user.LicenseNumber, user.Specialty,
            user.IsActive, user.CreatedAt, user.LastLoginAt, roles);
    }
}

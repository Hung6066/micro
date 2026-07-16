using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Users.Queries;

/// <summary>
/// Paginated user search query with filtering by role, search term, and active status.
/// </summary>
public record GetUsersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Role = null,
    bool? IsActive = null)
    : IRequest<PagedResult<UserDetailDto>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PagedResult<UserDetailDto>>
{
    private readonly UserManager<User> _userManager;

    public GetUsersQueryHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<PagedResult<UserDetailDto>> Handle(GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        IQueryable<User> query = _userManager.Users;

        // Apply search filter across name fields
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(search) ||
                u.LastName.ToLower().Contains(search) ||
                (u.MiddleName != null && u.MiddleName.ToLower().Contains(search)) ||
                u.Email!.ToLower().Contains(search) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));
        }

        // Filter by active status
        if (request.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == request.IsActive.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Role filtering done in-memory after pagination for simplicity
        var userDtos = new List<UserDetailDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            if (string.IsNullOrWhiteSpace(request.Role) || roles.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
            {
                userDtos.Add(MapToDto(user, roles));
            }
        }

        return new PagedResult<UserDetailDto>(userDtos, totalCount, request.Page, request.PageSize);
    }

    private static UserDetailDto MapToDto(User user, IList<string> roles) => new(
        user.Id, user.UserName!, user.Email!, user.PhoneNumber,
        user.FirstName, user.LastName, user.MiddleName,
        user.FullName, user.LicenseNumber, user.Specialty,
        user.IsActive, user.CreatedAt, user.LastLoginAt, roles);
}

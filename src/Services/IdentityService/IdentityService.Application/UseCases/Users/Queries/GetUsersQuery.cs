using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using His.Hope.IdentityService.Application.Interfaces;

namespace His.Hope.IdentityService.Application.UseCases.Users.Queries;

/// <summary>
/// Paginated user search query with filtering by role, search term, and active status.
/// </summary>
public record GetUsersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Role = null,
    bool? IsActive = null,
    string? Sort = null)
    : IRequest<PagedResult<UserDetailDto>>;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PagedResult<UserDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<UserDetailDto>> Handle(GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        IQueryable<User> query = _context.Users.AsNoTracking();

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

        // Resolve the role filter before counting and paging so the API contract
        // remains truthful for server-side pagination.
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var roleName = request.Role.Trim();
            query = query.Where(user => _context.UserRoles
                .Where(userRole => userRole.UserId == user.Id)
                .Join(_context.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => role.Name)
                .Any(name => name == roleName));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        var sort = request.Sort?.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var descending = sort?.Length > 1 && string.Equals(sort[1], "desc", StringComparison.OrdinalIgnoreCase);
        query = (sort?.FirstOrDefault()?.ToLowerInvariant(), descending) switch
        {
            ("username", false) => query.OrderBy(u => u.UserName),
            ("username", true) => query.OrderByDescending(u => u.UserName),
            ("email", false) => query.OrderBy(u => u.Email),
            ("email", true) => query.OrderByDescending(u => u.Email),
            ("isactive", false) => query.OrderBy(u => u.IsActive),
            ("isactive", true) => query.OrderByDescending(u => u.IsActive),
            ("createdat", false) => query.OrderBy(u => u.CreatedAt),
            _ => query.OrderByDescending(u => u.CreatedAt)
        };

        var users = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(user => user.Id).ToArray();
        var roleRows = await _context.UserRoles
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Join(_context.Roles, userRole => userRole.RoleId, role => role.Id,
                (userRole, role) => new { userRole.UserId, RoleName = role.Name! })
            .ToListAsync(cancellationToken);
        var rolesByUser = roleRows
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => (IList<string>)group.Select(row => row.RoleName).ToList());
        var userDtos = users
            .Select(user => MapToDto(user, rolesByUser.GetValueOrDefault(user.Id) ?? Array.Empty<string>()))
            .ToList();

        return new PagedResult<UserDetailDto>(userDtos, totalCount, request.Page, request.PageSize);
    }

    private static UserDetailDto MapToDto(User user, IList<string> roles) => new(
        user.Id, user.UserName!, user.Email!, user.PhoneNumber,
        user.FirstName, user.LastName, user.MiddleName,
        user.FullName, user.LicenseNumber, user.Specialty,
        user.IsActive, user.CreatedAt, user.LastLoginAt, roles, user.ConcurrencyStamp);
}

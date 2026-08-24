using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Roles.Queries;

public record GetRolesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Sort = null,
    IReadOnlyList<string>? TenantMembershipKeys = null)
    : IRequest<PagedResult<RoleDto>>;

public class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, PagedResult<RoleDto>>
{
    private readonly IApplicationDbContext _context;

    public GetRolesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Roles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(r => (r.Name ?? "").Contains(search) || (r.Description ?? "").Contains(search));
        }

        if (request.TenantMembershipKeys is { Count: > 0 } tenantKeys)
        {
            var normalizedKeys = tenantKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim().ToLowerInvariant())
                .Distinct()
                .ToArray();
            query = query.Where(role =>
                _context.UserRoles.Any(userRole =>
                    userRole.RoleId == role.Id &&
                    _context.UserClaims.Any(claim =>
                        claim.UserId == userRole.UserId &&
                        claim.ClaimType == "tenant_membership" &&
                        normalizedKeys.Contains(claim.ClaimValue.ToLower()))) ||
                _context.AccessRequests.Any(accessRequest =>
                    accessRequest.RoleIdsJson.Contains(role.Id.ToString()) &&
                    _context.UserClaims.Any(claim =>
                        claim.UserId == accessRequest.SubjectUserId &&
                        claim.ClaimType == "tenant_membership" &&
                        normalizedKeys.Contains(claim.ClaimValue.ToLower()))) ||
                _context.AccessReviews.Any(accessReview =>
                    accessReview.RoleIdsJson.Contains(role.Id.ToString()) &&
                    _context.UserClaims.Any(claim =>
                        claim.UserId == accessReview.SubjectUserId &&
                        claim.ClaimType == "tenant_membership" &&
                        normalizedKeys.Contains(claim.ClaimValue.ToLower()))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var sort = request.Sort?.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var descending = sort?.Length > 1 && string.Equals(sort[1], "desc", StringComparison.OrdinalIgnoreCase);
        query = (sort?.FirstOrDefault()?.ToLowerInvariant(), descending) switch
        {
            ("description", false) => query.OrderBy(r => r.Description),
            ("description", true) => query.OrderByDescending(r => r.Description),
            ("createdat", false) => query.OrderBy(r => r.CreatedAt),
            ("createdat", true) => query.OrderByDescending(r => r.CreatedAt),
            ("name", true) => query.OrderByDescending(r => r.Name),
            _ => query.OrderBy(r => r.Name)
        };

        var roles = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var roleIds = roles.Select(role => role.Id).ToArray();
        var rolePermissions = await _context.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => roleIds.Contains(rolePermission.RoleId))
            .Include(rolePermission => rolePermission.Permission)
            .ToListAsync(cancellationToken);
        var permissionsByRole = rolePermissions
            .GroupBy(rolePermission => rolePermission.RoleId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var items = roles.Select(r => new RoleDto(
            r.Id,
            r.Name!,
            r.Description,
            r.IsSystem,
            r.CreatedAt,
            permissionsByRole.GetValueOrDefault(r.Id, new List<RolePermission>()).Select(rp => new PermissionDto(
                rp.PermissionCode,
                rp.Permission.Name,
                rp.Permission.Group,
                rp.Permission.Description,
                rp.Permission.IsSystem
            )).ToList(),
            r.ConcurrencyStamp,
            r.Owner,
            r.AuthorizationVersion,
            r.RiskTier,
            r.ReviewCadenceDays,
            r.LifecycleStatus,
            r.PublishedAt,
            r.PublishedBy
        )).ToList();
        return new PagedResult<RoleDto>(items, totalCount, request.Page, request.PageSize);
    }
}

using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Settings.Queries;

public record GetSettingsQuery(string? ScopeId = null) : IRequest<List<SystemSettingDto>>;

public class GetSettingsQueryHandler : IRequestHandler<GetSettingsQuery, List<SystemSettingDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSettingsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SystemSettingDto>> Handle(GetSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var settings = await _context.SystemSettings.AsNoTracking()
            .TagWith("Identity.Settings.GetSettings")
            .Where(s => s.ScopeId == IdentityScope.Global || s.ScopeId == (request.ScopeId ?? IdentityScope.Global))
            .ToListAsync(cancellationToken);

        return settings
            .GroupBy(s => s.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(s => s.ScopeId == request.ScopeId && request.ScopeId != null).First())
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .Select(s => new SystemSettingDto(
                s.Key, s.Value, s.Description, s.Category, s.UpdatedAt, s.UpdatedBy, s.ScopeId))
            .ToList();
    }
}

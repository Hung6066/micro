using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Settings.Queries;

public record GetSettingsQuery : IRequest<List<SystemSettingDto>>;

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
        return await _context.SystemSettings
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .Select(s => new SystemSettingDto(
                s.Key, s.Value, s.Description, s.Category, s.UpdatedAt, s.UpdatedBy))
            .ToListAsync(cancellationToken);
    }
}

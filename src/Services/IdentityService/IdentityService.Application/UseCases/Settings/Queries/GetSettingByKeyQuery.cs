using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Settings.Queries;

public record GetSettingByKeyQuery(string Key) : IRequest<SystemSettingDto?>;

public class GetSettingByKeyQueryHandler : IRequestHandler<GetSettingByKeyQuery, SystemSettingDto?>
{
    private readonly IApplicationDbContext _context;

    public GetSettingByKeyQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SystemSettingDto?> Handle(GetSettingByKeyQuery request,
        CancellationToken cancellationToken)
    {
        var setting = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == request.Key, cancellationToken);

        if (setting is null) return null;

        return new SystemSettingDto(
            setting.Key, setting.Value, setting.Description,
            setting.Category, setting.UpdatedAt, setting.UpdatedBy);
    }
}

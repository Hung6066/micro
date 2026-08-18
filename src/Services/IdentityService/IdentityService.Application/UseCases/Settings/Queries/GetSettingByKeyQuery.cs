using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Settings.Queries;

public record GetSettingByKeyQuery(string Key, string? ScopeId = null) : IRequest<SystemSettingDto?>;

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
            .Where(s => s.Key == request.Key && (s.ScopeId == IdentityScope.Global || s.ScopeId == (request.ScopeId ?? IdentityScope.Global)))
            .OrderByDescending(s => s.ScopeId == request.ScopeId && request.ScopeId != null)
            .FirstOrDefaultAsync(cancellationToken);

        if (setting is null) return null;

        return new SystemSettingDto(
            setting.Key, setting.Value, setting.Description,
            setting.Category, setting.UpdatedAt, setting.UpdatedBy, setting.ScopeId);
    }
}

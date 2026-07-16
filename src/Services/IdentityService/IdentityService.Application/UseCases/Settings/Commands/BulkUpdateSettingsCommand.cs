using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Settings.Commands;

public record BulkUpdateSettingsCommand(
    List<BulkUpdateSettingItem> Settings,
    string? UpdatedBy)
    : IRequest<List<SystemSettingDto>>;

public class BulkUpdateSettingsCommandHandler
    : IRequestHandler<BulkUpdateSettingsCommand, List<SystemSettingDto>>
{
    private readonly IApplicationDbContext _context;

    public BulkUpdateSettingsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SystemSettingDto>> Handle(BulkUpdateSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var results = new List<SystemSettingDto>();

        foreach (var item in request.Settings)
        {
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == item.Key, cancellationToken);

            if (setting is null)
            {
                setting = new SystemSetting
                {
                    Key = item.Key,
                    Value = item.Value,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = request.UpdatedBy
                };
                _context.SystemSettings.Add(setting);
            }
            else
            {
                setting.Value = item.Value;
                setting.UpdatedAt = DateTime.UtcNow;
                setting.UpdatedBy = request.UpdatedBy;
            }

            results.Add(new SystemSettingDto(
                setting.Key, setting.Value, setting.Description,
                setting.Category, setting.UpdatedAt, setting.UpdatedBy));
        }

        await _context.SaveChangesAsync(cancellationToken);
        return results;
    }
}

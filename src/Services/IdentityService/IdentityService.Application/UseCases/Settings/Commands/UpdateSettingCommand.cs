using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Application.UseCases.Settings.Commands;

public record UpdateSettingCommand(
    string Key,
    string Value,
    string? Description,
    string? UpdatedBy)
    : IRequest<SystemSettingDto>;

public class UpdateSettingCommandHandler : IRequestHandler<UpdateSettingCommand, SystemSettingDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateSettingCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SystemSettingDto> Handle(UpdateSettingCommand request,
        CancellationToken cancellationToken)
    {
        var setting = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == request.Key, cancellationToken);

        if (setting is null)
        {
            // Create new setting if it doesn't exist
            setting = new SystemSetting
            {
                Key = request.Key,
                Value = request.Value,
                Description = request.Description,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = request.UpdatedBy
            };
            _context.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = request.Value;
            if (request.Description is not null)
                setting.Description = request.Description;
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedBy = request.UpdatedBy;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new SystemSettingDto(
            setting.Key, setting.Value, setting.Description,
            setting.Category, setting.UpdatedAt, setting.UpdatedBy);
    }
}

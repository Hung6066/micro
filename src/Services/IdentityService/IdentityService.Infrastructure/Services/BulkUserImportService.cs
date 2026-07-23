using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class BulkUserImportService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly ILogger<BulkUserImportService> _logger;

    public BulkUserImportService(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        ILogger<BulkUserImportService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<BulkImportResult> ImportAsync(BulkImportRequest request, CancellationToken ct = default)
    {
        var succeeded = 0;
        var skipped = 0;
        var failed = 0;
        var errors = new List<BulkImportError>();

        foreach (var record in request.Users)
        {
            try
            {
                var existing = await _userManager.FindByNameAsync(record.UserName)
                            ?? await _userManager.FindByEmailAsync(record.Email);

                if (existing is not null)
                {
                    if (request.SkipExisting)
                    {
                        skipped++;
                        continue;
                    }
                    existing.Email = record.Email;
                    existing.FirstName = record.FirstName;
                    existing.LastName = record.LastName;
                    existing.IsActive = record.IsActive;
                    existing.LicenseNumber = record.LicenseNumber;
                    existing.Specialty = record.Specialty;
                    await _userManager.UpdateAsync(existing);
                    succeeded++;
                    continue;
                }

                var user = new User
                {
                    UserName = record.UserName,
                    Email = record.Email,
                    FirstName = record.FirstName,
                    LastName = record.LastName,
                    MiddleName = record.MiddleName,
                    LicenseNumber = record.LicenseNumber,
                    Specialty = record.Specialty,
                    IsActive = record.IsActive,
                    EmailConfirmed = false,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    failed++;
                    errors.Add(new BulkImportError(record.UserName,
                        string.Join("; ", createResult.Errors.Select(e => e.Description))));
                    continue;
                }

                if (!string.IsNullOrEmpty(record.Role))
                {
                    if (await _roleManager.RoleExistsAsync(record.Role))
                        await _userManager.AddToRoleAsync(user, record.Role);
                }

                succeeded++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk import failed for user {UserName}", record.UserName);
                failed++;
                errors.Add(new BulkImportError(record.UserName, ex.Message));
            }
        }

        var result = new BulkImportResult(request.Users.Count, succeeded, skipped, failed, errors);

        _logger.LogInformation("Bulk import complete: {Succeeded} succeeded, {Skipped} skipped, {Failed} failed out of {Total}",
            result.Succeeded, result.Skipped, result.Failed, result.TotalSubmitted);

        return result;
    }
}

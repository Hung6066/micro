using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class BulkUserImportService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly ILogger<BulkUserImportService> _logger;
    private readonly IApplicationDbContext _context;

    public BulkUserImportService(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        ILogger<BulkUserImportService> logger,
        IApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
        _context = context;
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
                    await UpsertFacilityAsync(existing, record.FacilityId, ct);
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

                await UpsertFacilityAsync(user, record.FacilityId, ct);

                succeeded++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk import failed for a user record");
                failed++;
                errors.Add(new BulkImportError(record.UserName, ex.Message));
            }
        }

        var result = new BulkImportResult(request.Users.Count, succeeded, skipped, failed, errors);

        _logger.LogInformation("Bulk import complete: {Succeeded} succeeded, {Skipped} skipped, {Failed} failed out of {Total}",
            result.Succeeded, result.Skipped, result.Failed, result.TotalSubmitted);

        return result;
    }

    private async Task UpsertFacilityAsync(User user, string? facilityId, CancellationToken ct)
    {
        var normalized = facilityId?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        var memberships = await _context.UserFacilities
            .Where(membership => membership.UserId == user.Id && membership.IsActive)
            .ToListAsync(ct);
        foreach (var membership in memberships)
            membership.IsPrimary = false;

        var current = memberships.FirstOrDefault(membership =>
            string.Equals(membership.FacilityId, normalized, StringComparison.OrdinalIgnoreCase));
        if (current is null)
        {
            _context.UserFacilities.Add(new UserFacility
            {
                UserId = user.Id,
                FacilityId = normalized,
                IsPrimary = true,
                IsActive = true
            });
        }
        else
        {
            current.IsPrimary = true;
            current.IsActive = true;
            current.RevokedAt = null;
        }

        await _context.SaveChangesAsync(ct);
    }
}

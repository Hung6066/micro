using System.Text.Json;
using His.Hope.IdentityService.Application.Provisioning;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public sealed class DirectoryProvisioningDispatcher(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DirectoryProvisioningDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Directory provisioning dispatch cycle failed"); }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task DispatchAsync(CancellationToken ct)
    {
        var mode = (configuration["PROVISIONING_MODE"] ?? "dry-run").Trim().ToLowerInvariant();
        if (mode is "disabled" or "off") return;
        if (mode is not ("dry-run" or "observe" or "enabled" or "live"))
        {
            logger.LogWarning("Ignoring provisioning dispatch because PROVISIONING_MODE value '{Mode}' is not recognized", mode);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var targets = scope.ServiceProvider.GetServices<IProvisioningTarget>();
        var targetMap = targets.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var entries = await db.DirectoryProvisioningOutbox
            .Where(item => item.CompletedAt == null && item.AvailableAt <= DateTime.UtcNow)
            .OrderBy(item => item.CreatedAt).Take(50).ToListAsync(ct);
        foreach (var entry in entries)
        {
            // Dry-run is an explicit safety mode: acknowledge the durable job
            // without contacting any vendor or SCIM endpoint.
            if (mode is "dry-run" or "observe")
            {
                entry.Attempts++;
                entry.CompletedAt = DateTime.UtcNow;
                entry.LastError = "dry_run_no_external_call";
                continue;
            }

            if (!targetMap.TryGetValue(entry.Target, out var target))
            {
                entry.LastError = $"Unknown provisioning target '{entry.Target}'.";
                entry.Attempts++;
                entry.AvailableAt = DateTime.UtcNow.AddMinutes(30);
                continue;
            }
            try
            {
                if (string.IsNullOrWhiteSpace(entry.ExternalId))
                    entry.ExternalId = await db.DirectoryProvisioningBindings
                        .Where(binding => binding.Target == entry.Target && binding.ResourceType == entry.ResourceType && binding.ResourceId == entry.ResourceId)
                        .Select(binding => binding.ExternalId)
                        .SingleOrDefaultAsync(ct);
                using var payload = JsonDocument.Parse(entry.PayloadJson);
                var result = await target.ApplyAsync(new ProvisioningChange(entry.Target, entry.Operation, entry.ResourceType, entry.ResourceId, payload, entry.ExternalId), ct);
                entry.Attempts++;
                if (result.Success)
                {
                    entry.CompletedAt = DateTime.UtcNow;
                    entry.ExternalId ??= result.ExternalId;
                    if (!string.IsNullOrWhiteSpace(result.ExternalId))
                    {
                        var binding = await db.DirectoryProvisioningBindings.SingleOrDefaultAsync(item =>
                            item.Target == entry.Target && item.ResourceType == entry.ResourceType && item.ResourceId == entry.ResourceId, ct);
                        if (binding is null)
                            db.DirectoryProvisioningBindings.Add(new DirectoryProvisioningBinding
                            {
                                Target = entry.Target,
                                ResourceType = entry.ResourceType,
                                ResourceId = entry.ResourceId,
                                ExternalId = result.ExternalId
                            });
                        else
                        {
                            binding.ExternalId = result.ExternalId;
                            binding.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                    if (entry.Operation.Equals("delete", StringComparison.OrdinalIgnoreCase))
                    {
                        var binding = await db.DirectoryProvisioningBindings.SingleOrDefaultAsync(item =>
                            item.Target == entry.Target && item.ResourceType == entry.ResourceType && item.ResourceId == entry.ResourceId, ct);
                        if (binding is not null) db.DirectoryProvisioningBindings.Remove(binding);
                    }
                    entry.LastError = null;
                }
                else
                {
                    entry.LastError = result.Error;
                    entry.AvailableAt = DateTime.UtcNow.AddMinutes(Math.Min(entry.Attempts, 30));
                }
            }
            catch (Exception ex)
            {
                entry.Attempts++;
                entry.LastError = ex.Message[..Math.Min(ex.Message.Length, 2000)];
                entry.AvailableAt = DateTime.UtcNow.AddMinutes(Math.Min(entry.Attempts, 30));
            }
        }
        await db.SaveChangesAsync(ct);
    }
}

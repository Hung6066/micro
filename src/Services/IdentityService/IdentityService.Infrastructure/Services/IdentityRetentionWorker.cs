using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace His.Hope.IdentityService.Infrastructure.Services;

public sealed class IdentityRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IdentityRedisLock distributedLock,
    IOptions<IdentityRetentionOptions> options,
    ILogger<IdentityRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Identity retention cleanup failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(Math.Clamp(options.Value.IntervalMinutes, 1, 1440)), stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        var settings = options.Value;
        await using var lease = await distributedLock.TryAcquireAsync(
            "hishop:identity:retention-cleanup",
            TimeSpan.FromMinutes(Math.Clamp(settings.LockTtlMinutes, 1, 60)));
        if (lease is null) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var now = DateTime.UtcNow;
        var deleted = 0;
        deleted += await DeleteInBatchesAsync(db.DirectoryProvisioningOutbox
            .Where(item => item.CompletedAt != null && item.CompletedAt < now.AddDays(-Math.Max(1, settings.CompletedOutboxDays))),
            settings.BatchSize, ct);
        deleted += await DeleteInBatchesAsync(db.SecuritySignalOutbox
            .Where(item => item.DispatchedAt != null && item.DispatchedAt < now.AddDays(-Math.Max(1, settings.CompletedOutboxDays))),
            settings.BatchSize, ct);
        deleted += await DeleteInBatchesAsync(db.PushNotificationOutbox
            .Where(item => item.ProcessedAt != null && item.ProcessedAt < now.AddDays(-Math.Max(1, settings.ProcessedPushDays))),
            settings.BatchSize, ct);
        deleted += await DeleteInBatchesAsync(db.MobileTelemetryEvents
            .Where(item => item.CreatedAt < now.AddDays(-Math.Max(1, settings.TelemetryDays))),
            settings.BatchSize, ct);
        deleted += await DeleteInBatchesAsync(db.SecurityEvents
            .Where(item => item.Timestamp < now.AddDays(-Math.Max(1, settings.SecurityEventDays))),
            settings.BatchSize, ct);
        deleted += await DeleteInBatchesAsync(db.DevicePostureAssessments
            .Where(item => item.CreatedAt < now.AddDays(-Math.Max(1, settings.DevicePostureDays))),
            settings.BatchSize, ct);
        if (deleted > 0) logger.LogInformation("Identity retention cleanup removed {Count} records", deleted);
    }

    private static async Task<int> DeleteInBatchesAsync<TEntity>(IQueryable<TEntity> query, int configuredBatchSize, CancellationToken ct)
        where TEntity : class
    {
        var batchSize = Math.Clamp(configuredBatchSize, 1, 10_000);
        var total = 0;
        while (true)
        {
            var batch = await query.Take(batchSize).ExecuteDeleteAsync(ct);
            total += batch;
            if (batch < batchSize) return total;
        }
    }
}
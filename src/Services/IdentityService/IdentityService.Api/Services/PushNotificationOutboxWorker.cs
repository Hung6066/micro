using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Services;

/// <summary>Drains durable push work and retries transient provider failures.</summary>
public sealed class PushNotificationOutboxWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PushNotificationOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessOneAsync(stoppingToken);
                if (!processed) await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Push notification outbox iteration failed");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var now = DateTime.UtcNow;
        var candidate = await db.PushNotificationOutbox
            .Where(item => item.ProcessedAt == null && item.AvailableAt <= now &&
                (item.LeaseUntil == null || item.LeaseUntil < now))
            .OrderBy(item => item.CreatedAt)
            .Select(item => new { item.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return false;

        var leaseId = Guid.NewGuid();
        var leaseUntil = now.AddMinutes(2);
        var claimed = await db.PushNotificationOutbox
            .Where(item => item.Id == candidate.Id && item.ProcessedAt == null &&
                (item.LeaseUntil == null || item.LeaseUntil < now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.AttemptCount, item => item.AttemptCount + 1)
                .SetProperty(item => item.LeaseId, leaseId)
                .SetProperty(item => item.LeaseUntil, leaseUntil), cancellationToken);
        if (claimed == 0) return true;

        var item = await db.PushNotificationOutbox
            .SingleAsync(notification => notification.Id == candidate.Id, cancellationToken);

        var delivery = scope.ServiceProvider.GetRequiredService<IPushDeliveryService>();
        var delivered = await delivery.DeliverAsync(item.UserId, item.Title, item.Body, cancellationToken);
        if (delivered)
        {
            item.ProcessedAt = DateTime.UtcNow;
            item.LastError = null;
        }
        else
        {
            var backoffMinutes = Math.Min(30, Math.Pow(2, Math.Min(item.AttemptCount, 5)));
            item.AvailableAt = DateTime.UtcNow.AddMinutes(backoffMinutes);
            item.LastError = "No active device accepted the notification";
        }
        item.LeaseId = null;
        item.LeaseUntil = null;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

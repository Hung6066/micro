using His.Hope.ContentService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.ContentService.Infrastructure.Messaging;

internal static class ContentPublishingOutboxClaiming
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);

    public static async Task<ContentPublishingOutboxEntity?> ClaimAsync(ContentDbContext db, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var message = await db.PublishingOutbox
            .FromSqlRaw("""
                SELECT * FROM content_publishing_outbox
                WHERE processed_on IS NULL
                  AND (lease_until IS NULL OR lease_until <= CURRENT_TIMESTAMP)
                ORDER BY occurred_at
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .FirstOrDefaultAsync(cancellationToken);

        if (message is null)
            return null;

        var now = DateTimeOffset.UtcNow;
        message.AttemptCount++;
        message.LastAttemptedAt = now;
        message.LeaseUntil = now.Add(LeaseDuration);
        message.LastError = null;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return message;
    }
}

using Microsoft.EntityFrameworkCore;

namespace His.Hope.Infrastructure.Idempotency;

/// <summary>
/// Atomically records an event/consumer pair before handler execution.
/// The composite key makes duplicate deliveries safe across replicas.
/// </summary>
public sealed class InboxDeduplicator
{
    private readonly IdempotencyDbContext _dbContext;

    public InboxDeduplicator(IdempotencyDbContext dbContext) => _dbContext = dbContext;

    public async Task<bool> TryBeginAsync(Guid eventId, string consumer, CancellationToken ct = default)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("Event id is required.", nameof(eventId));
        if (string.IsNullOrWhiteSpace(consumer)) throw new ArgumentException("Consumer is required.", nameof(consumer));

        _dbContext.ProcessedEvents.Add(new ProcessedEvent
        {
            EventId = eventId,
            Consumer = consumer,
            ProcessedAt = DateTime.UtcNow
        });

        try
        {
            await _dbContext.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _dbContext.Entry(_dbContext.ProcessedEvents.Local.Single(e =>
                e.EventId == eventId && e.Consumer == consumer)).State = EntityState.Detached;
            return false;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true ||
        exception.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true;
}

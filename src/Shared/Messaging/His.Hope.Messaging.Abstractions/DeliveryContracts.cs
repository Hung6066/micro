namespace His.Hope.Messaging;

public sealed record OutboxMessage(
    Guid Id,
    EventEnvelope Event,
    DateTimeOffset AvailableAt,
    int AttemptCount = 0,
    DateTimeOffset? PublishedAt = null,
    string? LastError = null);

public interface IOutboxStore
{
    ValueTask EnqueueAsync(EventEnvelope @event, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<OutboxMessage>> ReadPendingAsync(int maxCount, CancellationToken cancellationToken = default);
    ValueTask MarkPublishedAsync(Guid messageId, CancellationToken cancellationToken = default);
    ValueTask MarkFailedAsync(Guid messageId, string error, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken = default);
}

public interface IMessagePublisher
{
    ValueTask PublishAsync(EventEnvelope @event, CancellationToken cancellationToken = default);
}

public interface IInboxStore
{
    ValueTask<bool> TryBeginAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default);
    ValueTask MarkCompletedAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default);
    ValueTask ReleaseAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default);
}

public sealed record IdempotencyRecord(
    string Key,
    string RequestFingerprint,
    int? StatusCode = null,
    string? Response = null,
    DateTimeOffset? CompletedAt = null);

public interface IIdempotencyStore
{
    ValueTask<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken = default);
    ValueTask<bool> TryBeginAsync(string key, string requestFingerprint, CancellationToken cancellationToken = default);
    ValueTask CompleteAsync(string key, int statusCode, string response, CancellationToken cancellationToken = default);
}

using System.Collections.Concurrent;

namespace His.Hope.Messaging;

public sealed class InMemoryOutboxStore : IOutboxStore
{
    private readonly ConcurrentDictionary<Guid, OutboxMessage> _messages = new();

    public ValueTask EnqueueAsync(EventEnvelope @event, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EventDeliveryPolicy.Default.Validate(@event);
        var message = new OutboxMessage(@event.Id, @event, DateTimeOffset.UtcNow);
        _messages.TryAdd(message.Id, message);
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<OutboxMessage>> ReadPendingAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (maxCount < 1) throw new ArgumentOutOfRangeException(nameof(maxCount));
        var now = DateTimeOffset.UtcNow;
        IReadOnlyList<OutboxMessage> result = _messages.Values
            .Where(message => message.PublishedAt is null && message.AvailableAt <= now)
            .OrderBy(message => message.AvailableAt)
            .Take(maxCount)
            .ToArray();
        return ValueTask.FromResult(result);
    }

    public ValueTask MarkPublishedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _messages.AddOrUpdate(messageId,
            _ => throw new KeyNotFoundException($"Outbox message '{messageId}' was not found."),
            (_, current) => current with { PublishedAt = DateTimeOffset.UtcNow, LastError = null });
        return ValueTask.CompletedTask;
    }

    public ValueTask MarkFailedAsync(Guid messageId, string error, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        _messages.AddOrUpdate(messageId,
            _ => throw new KeyNotFoundException($"Outbox message '{messageId}' was not found."),
            (_, current) => current with
            {
                AttemptCount = current.AttemptCount + 1,
                AvailableAt = nextAttemptAt,
                LastError = error
            });
        return ValueTask.CompletedTask;
    }
}

public sealed class InMemoryInboxStore : IInboxStore
{
    private readonly ConcurrentDictionary<(Guid EventId, string Consumer), byte> _started = new();

    public ValueTask<bool> TryBeginAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (eventId == Guid.Empty) throw new ArgumentException("Event id is required.", nameof(eventId));
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);
        return ValueTask.FromResult(_started.TryAdd((eventId, consumer), 0));
    }

    public ValueTask MarkCompletedAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    public ValueTask ReleaseAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _started.TryRemove((eventId, consumer), out _);
        return ValueTask.CompletedTask;
    }
}

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyRecord> _records = new(StringComparer.Ordinal);

    public ValueTask<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_records.TryGetValue(key, out var record) ? record : null);
    }

    public ValueTask<bool> TryBeginAsync(string key, string requestFingerprint, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        return ValueTask.FromResult(_records.TryAdd(key, new IdempotencyRecord(key, requestFingerprint)));
    }

    public ValueTask CompleteAsync(string key, int statusCode, string response, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(response);
        _records.AddOrUpdate(key,
            _ => throw new KeyNotFoundException($"Idempotency key '{key}' was not started."),
            (_, current) => current with { StatusCode = statusCode, Response = response, CompletedAt = DateTimeOffset.UtcNow });
        return ValueTask.CompletedTask;
    }
}

public sealed class InMemoryDurableJobStore : IDurableJobStore
{
    private readonly ConcurrentDictionary<Guid, DurableJob> _jobs = new();

    public ValueTask<bool> EnqueueAsync(DurableJob job, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(job.JobType);
        ArgumentException.ThrowIfNullOrWhiteSpace(job.Payload);
        return ValueTask.FromResult(_jobs.TryAdd(job.Id, job with { Status = DurableJobStatus.Queued }));
    }

    public ValueTask<DurableJob?> TryClaimAsync(string workerId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        foreach (var candidate in _jobs.Values
                     .Where(job => job.Status == DurableJobStatus.Queued && (job.AvailableAt is null || job.AvailableAt <= now))
                     .OrderBy(job => job.AvailableAt ?? DateTimeOffset.MinValue))
        {
            var claimed = candidate with { Status = DurableJobStatus.Running, AttemptCount = candidate.AttemptCount + 1, WorkerId = workerId };
            if (_jobs.TryUpdate(candidate.Id, claimed, candidate)) return ValueTask.FromResult<DurableJob?>(claimed);
        }
        return ValueTask.FromResult<DurableJob?>(null);
    }

    public ValueTask CompleteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Update(jobId, job => job with { Status = DurableJobStatus.Completed, CompletedAt = DateTimeOffset.UtcNow });
        return ValueTask.CompletedTask;
    }

    public ValueTask RetryAsync(Guid jobId, string error, DateTimeOffset nextAttemptAt, int maxAttempts, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        Update(jobId, job => job with
        {
            Status = job.AttemptCount >= maxAttempts ? DurableJobStatus.DeadLettered : DurableJobStatus.Queued,
            AvailableAt = nextAttemptAt,
            LastError = error,
            WorkerId = null
        });
        return ValueTask.CompletedTask;
    }

    public ValueTask<DurableJob?> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_jobs.TryGetValue(jobId, out var job) ? job : null);
    }

    private void Update(Guid jobId, Func<DurableJob, DurableJob> update)
    {
        while (true)
        {
            if (!_jobs.TryGetValue(jobId, out var current)) throw new KeyNotFoundException($"Job '{jobId}' was not found.");
            if (_jobs.TryUpdate(jobId, update(current), current)) return;
        }
    }
}

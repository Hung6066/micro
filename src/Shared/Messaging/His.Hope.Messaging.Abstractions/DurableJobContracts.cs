namespace His.Hope.Messaging;

public enum DurableJobStatus
{
    Queued,
    Running,
    Completed,
    DeadLettered
}

public sealed record DurableJob(
    Guid Id,
    string JobType,
    string Payload,
    DurableJobStatus Status = DurableJobStatus.Queued,
    int AttemptCount = 0,
    DateTimeOffset? AvailableAt = null,
    DateTimeOffset? CompletedAt = null,
    string? LastError = null,
    string? WorkerId = null);

public interface IDurableJobStore
{
    ValueTask<bool> EnqueueAsync(DurableJob job, CancellationToken cancellationToken = default);
    ValueTask<DurableJob?> TryClaimAsync(string workerId, DateTimeOffset now, CancellationToken cancellationToken = default);
    ValueTask CompleteAsync(Guid jobId, CancellationToken cancellationToken = default);
    ValueTask RetryAsync(Guid jobId, string error, DateTimeOffset nextAttemptAt, int maxAttempts, CancellationToken cancellationToken = default);
    ValueTask<DurableJob?> GetAsync(Guid jobId, CancellationToken cancellationToken = default);
}

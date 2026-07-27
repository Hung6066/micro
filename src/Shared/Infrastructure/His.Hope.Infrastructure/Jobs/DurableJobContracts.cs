using StackExchange.Redis;

namespace His.Hope.Infrastructure.Jobs;

public static class DurableJobStatus
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string DeadLetter = "DeadLetter";
}

public sealed class DurableJobOptions
{
    public string StreamKey { get; init; } = "his-hope:jobs";
    public string ConsumerGroup { get; init; } = "workers";
    public TimeSpan VisibilityTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan StateTtl { get; init; } = TimeSpan.FromDays(7);
    public int MaxAttempts { get; init; } = 3;
}

public sealed class DurableJobState
{
    public string JobId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = DurableJobStatus.Queued;
    public int Attempt { get; set; }
    public int Processed { get; set; }
    public int Total { get; set; }
    public int Progress { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record DurableJobMessage(string JobId, RedisValue MessageId);

using System.Text.Json;
using System.Text.Json.Serialization;
using StackExchange.Redis;

namespace His.Hope.DatabaseContinuityService;

public enum ContinuityJobStatus { Queued, Running, Completed, Failed }

public sealed class ContinuityJob
{
    public string JobId { get; set; } = Guid.NewGuid().ToString("N");
    public string Operation { get; set; } = "restore-drill";
    public string TargetEnvironment { get; set; } = "isolated";
    public DateTimeOffset? RestorePoint { get; set; }
    public ContinuityJobStatus Status { get; set; } = ContinuityJobStatus.Queued;
    public string? ErrorCode { get; set; }
    public string? ResultJson { get; set; }
    public int Attempt { get; set; }
    public string ActorSubject { get; set; } = "scheduler";
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ContinuityJobStore(IConnectionMultiplexer redis, ContinuityAuditStore audit)
{
    private const string Stream = "his-hope:database-continuity:jobs";
    private const string Group = "database-continuity-workers";
    private static readonly TimeSpan StateTtl = TimeSpan.FromDays(30);
    private static readonly TimeSpan VisibilityTimeout = TimeSpan.FromMinutes(5);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task EnqueueAsync(ContinuityJob job, CancellationToken ct)
    {
        await SaveAsync(job, ct);
        await audit.UpsertAsync(job, ct);
        await redis.GetDatabase().StringSetAsync(LatestKey, job.JobId, StateTtl);
        await redis.GetDatabase().StreamAddAsync(Stream, [new NameValueEntry("jobId", job.JobId)]);
    }

    public async Task<ContinuityJob?> GetAsync(string id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var value = await redis.GetDatabase().StringGetAsync(StateKey(id));
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<ContinuityJob>(value!, _json);
    }

    public async Task<ContinuityJob?> GetLatestAsync(CancellationToken ct)
    {
        var id = await redis.GetDatabase().StringGetAsync(LatestKey);
        return id.IsNullOrEmpty ? null : await GetAsync(id!, ct);
    }

    public async Task SaveAsync(ContinuityJob job, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await redis.GetDatabase().StringSetAsync(StateKey(job.JobId), JsonSerializer.Serialize(job, _json), StateTtl);
    }

    public async Task EnsureGroupAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try { await redis.GetDatabase().StreamCreateConsumerGroupAsync(Stream, Group, "0-0", true); }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase)) { }
    }

    public async Task<(string JobId, RedisValue MessageId)?> ReadAsync(string consumer, CancellationToken ct)
    {
        await EnsureGroupAsync(ct);
        var reclaimed = await redis.GetDatabase().StreamAutoClaimAsync(
            Stream, Group, consumer, (long)VisibilityTimeout.TotalMilliseconds, "0-0", 1);
        if (reclaimed.ClaimedEntries.Length > 0)
        {
            var reclaimedJobId = reclaimed.ClaimedEntries[0].Values.FirstOrDefault(x => x.Name == "jobId").Value;
            if (!reclaimedJobId.IsNullOrEmpty)
                return (reclaimedJobId.ToString(), reclaimed.ClaimedEntries[0].Id);
        }
        var entries = await redis.GetDatabase().StreamReadGroupAsync(Stream, Group, consumer, ">", 1);
        if (entries.Length == 0) return null;
        var jobId = entries[0].Values.FirstOrDefault(x => x.Name == "jobId").Value;
        return jobId.IsNullOrEmpty ? null : (jobId.ToString(), entries[0].Id);
    }

    public Task AckAsync(RedisValue messageId) => redis.GetDatabase().StreamAcknowledgeAsync(Stream, Group, messageId);

    public async Task RetryAsync(ContinuityJob job, string error, int maxAttempts, CancellationToken ct)
    {
        job.ErrorCode = error;
        if (job.Attempt >= maxAttempts)
        {
            job.Status = ContinuityJobStatus.Failed;
            await SaveAsync(job, ct);
            await redis.GetDatabase().StreamAddAsync(
                DeadLetterStream,
                [new NameValueEntry("jobId", job.JobId), new NameValueEntry("error", error)]);
            return;
        }

        job.Status = ContinuityJobStatus.Queued;
        await SaveAsync(job, ct);
        await redis.GetDatabase().StreamAddAsync(Stream, [new NameValueEntry("jobId", job.JobId)]);
    }

    public async Task<bool> AcquireSchedulerLockAsync(string owner, TimeSpan ttl)
    {
        return await redis.GetDatabase().StringSetAsync(
            "his-hope:database-continuity:scheduler-lock", owner, ttl, When.NotExists);
    }

    public async Task<DateTimeOffset?> GetLastScheduledAtAsync(string operation)
    {
        var value = await redis.GetDatabase().StringGetAsync(ScheduledKey(operation));
        return DateTimeOffset.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    public Task MarkScheduledAsync(string operation, DateTimeOffset value) =>
        redis.GetDatabase().StringSetAsync(ScheduledKey(operation), value.ToString("O"), StateTtl);

    public Task MarkCompletedAsync(string operation, DateTimeOffset value) =>
        redis.GetDatabase().StringSetAsync(CompletedKey(operation), value.ToString("O"), StateTtl);

    public async Task<DateTimeOffset?> GetLastCompletedAtAsync(string operation)
    {
        var value = await redis.GetDatabase().StringGetAsync(CompletedKey(operation));
        return DateTimeOffset.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static string StateKey(string id) => $"his-hope:database-continuity:job:{id}";
    private static string ScheduledKey(string operation) => $"his-hope:database-continuity:last-scheduled:{operation}";
    private static string CompletedKey(string operation) => $"his-hope:database-continuity:last-completed:{operation}";
    private const string LatestKey = "his-hope:database-continuity:latest";
    private const string DeadLetterStream = "his-hope:database-continuity:dead-letter";
}

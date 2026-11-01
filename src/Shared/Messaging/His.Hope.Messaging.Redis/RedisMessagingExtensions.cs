using System.Text.Json;
using His.Hope.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace His.Hope.Messaging.Redis;

public static class RedisMessagingExtensions
{
    public static IServiceCollection AddHisHopeRedisMessaging(this IServiceCollection services)
    {
        services.TryAddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        services.TryAddSingleton<IInboxStore, RedisInboxStore>();
        services.TryAddSingleton<IDurableJobStore, RedisDurableJobStore>();
        return services;
    }
}

public sealed class RedisInboxStore(IConnectionMultiplexer redis) : IInboxStore
{
    private static readonly TimeSpan DeliveryTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(10);
    private readonly IDatabase _database = redis.GetDatabase();

    public async ValueTask<bool> TryBeginAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (eventId == Guid.Empty) throw new ArgumentException("Event id is required.", nameof(eventId));
        if (string.IsNullOrWhiteSpace(consumer)) throw new ArgumentException("Consumer is required.", nameof(consumer));

        return await _database.StringSetAsync(
            Key(eventId, consumer), "processing", ProcessingLease, When.NotExists);
    }

    public async ValueTask MarkCompletedAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.StringSetAsync(Key(eventId, consumer), "completed", DeliveryTtl, When.Exists);
    }

    public async ValueTask ReleaseAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.KeyDeleteAsync(Key(eventId, consumer));
    }

    private static RedisKey Key(Guid eventId, string consumer) =>
        $"his-hope:inbox:{eventId:D}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(consumer))).ToLowerInvariant()}";
}

internal sealed class RedisIdempotencyStore(IConnectionMultiplexer redis) : IIdempotencyStore
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(10);
    private readonly IDatabase _database = redis.GetDatabase();

    public async ValueTask<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _database.StringGetAsync(Key(key));
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<IdempotencyRecord>(value!);
    }

    public async ValueTask<bool> TryBeginAsync(string key, string requestFingerprint, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var record = new IdempotencyRecord(key, requestFingerprint);
        return await _database.StringSetAsync(Key(key), JsonSerializer.Serialize(record), Lease, When.NotExists);
    }

    public async ValueTask CompleteAsync(string key, int statusCode, string response, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = await GetAsync(key, cancellationToken)
            ?? throw new InvalidOperationException($"Idempotency key '{key}' was not started.");
        var completed = current with { StatusCode = statusCode, Response = response, CompletedAt = DateTimeOffset.UtcNow };
        await _database.StringSetAsync(Key(key), JsonSerializer.Serialize(completed), Lease);
    }

    private static RedisKey Key(string key) => $"his-hope:idempotency:{key}";
}

internal sealed class RedisDurableJobStore(IConnectionMultiplexer redis) : IDurableJobStore
{
    private const string QueueKey = "his-hope:jobs:queue";
    private const string StatePrefix = "his-hope:jobs:state:";
    private static readonly TimeSpan StateTtl = TimeSpan.FromDays(7);
    private readonly IDatabase _database = redis.GetDatabase();

    public async ValueTask<bool> EnqueueAsync(DurableJob job, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var queued = job with { Status = DurableJobStatus.Queued, AvailableAt = job.AvailableAt ?? DateTimeOffset.UtcNow };
        var stateKey = StateKey(job.Id);
        var transaction = _database.CreateTransaction();
        transaction.AddCondition(Condition.KeyNotExists(stateKey));
        _ = transaction.StringSetAsync(stateKey, JsonSerializer.Serialize(queued), StateTtl);
        _ = transaction.SortedSetAddAsync(
            QueueKey,
            job.Id.ToString("D"),
            queued.AvailableAt.Value.ToUnixTimeMilliseconds());
        return await transaction.ExecuteAsync();
    }

    public async ValueTask<DurableJob?> TryClaimAsync(string workerId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ids = await _database.SortedSetRangeByScoreAsync(QueueKey, double.NegativeInfinity, now.ToUnixTimeMilliseconds(), Exclude.None, Order.Ascending, 0, 1);
        if (ids.Length == 0) return null;
        var id = ids[0].ToString();
        var jobId = Guid.Parse(id);
        var stateKey = StateKey(jobId);
        var currentValue = await _database.StringGetAsync(stateKey);
        if (currentValue.IsNullOrEmpty) return null;
        var current = JsonSerializer.Deserialize<DurableJob>(currentValue!);
        if (current is null) return null;
        var claimed = current with { Status = DurableJobStatus.Running, WorkerId = workerId, AttemptCount = current.AttemptCount + 1 };
        var transaction = _database.CreateTransaction();
        transaction.AddCondition(Condition.SortedSetContains(QueueKey, id));
        transaction.AddCondition(Condition.StringEqual(stateKey, currentValue));
        _ = transaction.SortedSetRemoveAsync(QueueKey, id);
        _ = transaction.StringSetAsync(stateKey, JsonSerializer.Serialize(claimed), StateTtl);
        return await transaction.ExecuteAsync() ? claimed : null;
    }

    public async ValueTask CompleteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stateKey = StateKey(jobId);
        var currentValue = await _database.StringGetAsync(stateKey);
        if (currentValue.IsNullOrEmpty)
            throw new InvalidOperationException("Job not found.");
        var current = JsonSerializer.Deserialize<DurableJob>(currentValue!)
            ?? throw new InvalidOperationException("Job state is invalid.");
        var completed = current with { Status = DurableJobStatus.Completed, CompletedAt = DateTimeOffset.UtcNow };
        await SaveIfCurrentAsync(stateKey, currentValue, completed, cancellationToken);
    }

    public async ValueTask RetryAsync(Guid jobId, string error, DateTimeOffset nextAttemptAt, int maxAttempts, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stateKey = StateKey(jobId);
        var currentValue = await _database.StringGetAsync(stateKey);
        if (currentValue.IsNullOrEmpty)
            throw new InvalidOperationException("Job not found.");
        var current = JsonSerializer.Deserialize<DurableJob>(currentValue!)
            ?? throw new InvalidOperationException("Job state is invalid.");
        var status = current.AttemptCount >= maxAttempts ? DurableJobStatus.DeadLettered : DurableJobStatus.Queued;
        var updated = current with { Status = status, LastError = error, AvailableAt = nextAttemptAt };
        var transaction = _database.CreateTransaction();
        transaction.AddCondition(Condition.StringEqual(stateKey, currentValue));
        _ = transaction.StringSetAsync(stateKey, JsonSerializer.Serialize(updated), StateTtl);
        if (status == DurableJobStatus.Queued)
            _ = transaction.SortedSetAddAsync(QueueKey, jobId.ToString("D"), nextAttemptAt.ToUnixTimeMilliseconds());
        if (!await transaction.ExecuteAsync())
            throw new InvalidOperationException("Job state changed while retrying.");
    }

    public async ValueTask<DurableJob?> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _database.StringGetAsync(StateKey(jobId));
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<DurableJob>(value!);
    }

    private async Task SaveIfCurrentAsync(
        RedisKey stateKey,
        RedisValue currentValue,
        DurableJob job,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var transaction = _database.CreateTransaction();
        transaction.AddCondition(Condition.StringEqual(stateKey, currentValue));
        _ = transaction.StringSetAsync(stateKey, JsonSerializer.Serialize(job), StateTtl);
        if (!await transaction.ExecuteAsync())
            throw new InvalidOperationException("Job state changed while completing.");
    }

    private static RedisKey StateKey(Guid id) => $"{StatePrefix}{id:D}";
}

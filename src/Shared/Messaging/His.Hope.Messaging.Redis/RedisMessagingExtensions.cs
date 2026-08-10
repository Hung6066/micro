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
        services.TryAddSingleton<IDurableJobStore, RedisDurableJobStore>();
        return services;
    }
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
        var current = await GetAsync(key, cancellationToken) ?? new IdempotencyRecord(key, string.Empty);
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
        var created = await _database.StringSetAsync(StateKey(job.Id), JsonSerializer.Serialize(queued), StateTtl, When.NotExists);
        if (!created) return false;
        await _database.SortedSetAddAsync(QueueKey, job.Id.ToString("D"), queued.AvailableAt.Value.ToUnixTimeMilliseconds());
        return true;
    }

    public async ValueTask<DurableJob?> TryClaimAsync(string workerId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ids = await _database.SortedSetRangeByScoreAsync(QueueKey, double.NegativeInfinity, now.ToUnixTimeMilliseconds(), Exclude.None, Order.Ascending, 0, 1);
        if (ids.Length == 0) return null;
        var id = ids[0].ToString();
        if (!await _database.SortedSetRemoveAsync(QueueKey, id)) return null;
        var current = await GetAsync(Guid.Parse(id), cancellationToken);
        if (current is null) return null;
        var claimed = current with { Status = DurableJobStatus.Running, WorkerId = workerId, AttemptCount = current.AttemptCount + 1 };
        await SaveAsync(claimed, cancellationToken);
        return claimed;
    }

    public async ValueTask CompleteAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        await SaveAsync((await GetAsync(jobId, cancellationToken) ?? throw new InvalidOperationException("Job not found.")) with { Status = DurableJobStatus.Completed, CompletedAt = DateTimeOffset.UtcNow }, cancellationToken);

    public async ValueTask RetryAsync(Guid jobId, string error, DateTimeOffset nextAttemptAt, int maxAttempts, CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(jobId, cancellationToken) ?? throw new InvalidOperationException("Job not found.");
        var status = current.AttemptCount >= maxAttempts ? DurableJobStatus.DeadLettered : DurableJobStatus.Queued;
        var updated = current with { Status = status, LastError = error, AvailableAt = nextAttemptAt };
        await SaveAsync(updated, cancellationToken);
        if (status == DurableJobStatus.Queued) await _database.SortedSetAddAsync(QueueKey, jobId.ToString("D"), nextAttemptAt.ToUnixTimeMilliseconds());
    }

    public async ValueTask<DurableJob?> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _database.StringGetAsync(StateKey(jobId));
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<DurableJob>(value!);
    }

    private async Task SaveAsync(DurableJob job, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.StringSetAsync(StateKey(job.Id), JsonSerializer.Serialize(job), StateTtl);
    }

    private static RedisKey StateKey(Guid id) => $"{StatePrefix}{id:D}";
}

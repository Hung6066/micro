using System.Text.Json;
using System.Text.Json.Serialization;
using His.Hope.Contracts.Bulk;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Jobs;

public sealed class AdminJobState
{
    public string JobId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public string[] RowKeys { get; set; } = [];
    public string PayloadJson { get; set; } = "{}";
    public string ActorSubject { get; set; } = "system";
    public bool IsCrossFacility { get; set; }
    public string[] AuthorizedFacilities { get; set; } = [];
    public string[]? AllowedTenantKeys { get; set; }
    public string[]? AllowedClientIds { get; set; }
    public string? CorrelationId { get; set; }
    public BulkJobStatus Status { get; set; } = BulkJobStatus.Queued;
    public int Processed { get; set; }
    public int Total { get; set; }
    public Dictionary<string, BulkJobRowContract> RowProgress { get; set; } = [];
    public string? ErrorCode { get; set; }
    public string? ResultKey { get; set; }
    public string? ContentType { get; set; }
    public string? FileName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record AdminJobMessage(string JobId, RedisValue MessageId);

public sealed class RedisAdminJobStore
{
    private const string StreamKey = "his-hope:identity:admin-jobs";
    private const string ConsumerGroup = "identity-admin-workers";
    private static readonly TimeSpan StateTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan ResultTtl = TimeSpan.FromHours(24);
    private readonly IConnectionMultiplexer _redis;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public RedisAdminJobStore(IConnectionMultiplexer redis) => _redis = redis;

    public async Task EnsureConsumerGroupAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            await _redis.GetDatabase().StreamCreateConsumerGroupAsync(
                StreamKey, ConsumerGroup, "0-0", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
            // The group is shared by all Identity replicas and is expected to already exist.
        }
    }

    public async Task CreateAndEnqueueAsync(AdminJobState state, CancellationToken ct)
    {
        await SaveAsync(state, ct);
        if (!string.IsNullOrWhiteSpace(state.Kind))
            await _redis.GetDatabase().StringSetAsync($"his-hope:identity:admin-job-latest:{state.Kind}", state.JobId, StateTtl);
        await _redis.GetDatabase().StreamAddAsync(StreamKey, [
            new NameValueEntry("jobId", state.JobId)
        ]);
    }

    public async Task<AdminJobState?> GetLatestAsync(string kind, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var jobId = await _redis.GetDatabase().StringGetAsync($"his-hope:identity:admin-job-latest:{kind}");
        return jobId.IsNullOrEmpty ? null : await GetAsync(jobId!, ct);
    }

    public async Task<AdminJobState?> GetAsync(string jobId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var value = await _redis.GetDatabase().StringGetAsync(StateKey(jobId));
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<AdminJobState>(value!, _jsonOptions);
    }

    public async Task SaveAsync(AdminJobState state, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        state.UpdatedAt = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        await _redis.GetDatabase().StringSetAsync(StateKey(state.JobId), json, StateTtl);
    }

    public async Task SaveResultAsync(AdminJobState state, byte[] content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        state.ResultKey = $"his-hope:identity:admin-job-result:{state.JobId}";
        await _redis.GetDatabase().StringSetAsync(state.ResultKey, content, ResultTtl);
        await SaveAsync(state, ct);
    }

    public async Task<(byte[] Content, AdminJobState State)?> GetResultAsync(string jobId, CancellationToken ct)
    {
        var state = await GetAsync(jobId, ct);
        if (state?.ResultKey is null) return null;
        var result = await _redis.GetDatabase().StringGetAsync(state.ResultKey);
        return result.IsNull ? null : ((byte[])result!, state);
    }

    public async Task CancelAsync(AdminJobState state, CancellationToken ct)
    {
        if (state.Status is BulkJobStatus.Completed or BulkJobStatus.Failed or BulkJobStatus.Cancelled) return;
        state.Status = BulkJobStatus.Cancelled;
        state.ErrorCode = "job_cancelled";
        await SaveAsync(state, ct);
    }

    public async Task<AdminJobMessage?> ReadNextAsync(string consumer, CancellationToken ct)
    {
        await EnsureConsumerGroupAsync(ct);
        var database = _redis.GetDatabase();
        var reclaimed = await database.StreamAutoClaimAsync(
            StreamKey, ConsumerGroup, consumer, 30_000, "0-0", 1);
        if (reclaimed.ClaimedEntries.Length > 0)
        {
            var reclaimedJobId = reclaimed.ClaimedEntries[0].Values.FirstOrDefault(v => v.Name == "jobId").Value;
            if (!reclaimedJobId.IsNullOrEmpty)
                return new AdminJobMessage(reclaimedJobId!, reclaimed.ClaimedEntries[0].Id);
        }

        var entries = await database.StreamReadGroupAsync(
            StreamKey, ConsumerGroup, consumer, ">", 1);
        if (entries.Length == 0) return null;
        var jobId = entries[0].Values.FirstOrDefault(v => v.Name == "jobId").Value;
        return jobId.IsNullOrEmpty ? null : new AdminJobMessage(jobId!, entries[0].Id);
    }

    public Task AcknowledgeAsync(RedisValue messageId) =>
        _redis.GetDatabase().StreamAcknowledgeAsync(StreamKey, ConsumerGroup, messageId);

    private static RedisKey StateKey(string jobId) => $"his-hope:identity:admin-job:{jobId}";
}

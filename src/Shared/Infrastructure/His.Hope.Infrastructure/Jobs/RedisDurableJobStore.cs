using System.Text.Json;
using StackExchange.Redis;

namespace His.Hope.Infrastructure.Jobs;

public sealed class RedisDurableJobStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly DurableJobOptions _options;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public RedisDurableJobStore(IConnectionMultiplexer redis, DurableJobOptions? options = null)
    {
        _redis = redis;
        _options = options ?? new DurableJobOptions();
        if (_options.MaxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(options));
    }

    public async Task EnsureConsumerGroupAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            await Database.StreamCreateConsumerGroupAsync(_options.StreamKey, _options.ConsumerGroup, "0-0", true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    public async Task<bool> EnqueueAsync(DurableJobState state, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        state.Status = DurableJobStatus.Queued;
        state.UpdatedAt = DateTimeOffset.UtcNow;
        var stateKey = StateKey(state.JobId);
        var result = await Database.ScriptEvaluateAsync(
            "if redis.call('EXISTS', KEYS[1]) == 1 then return 0 end " +
            "redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2]) " +
            "redis.call('SET', KEYS[3], ARGV[4], 'PX', ARGV[2]) " +
            "return redis.call('XADD', KEYS[2], '*', 'jobId', ARGV[3])",
            [stateKey, _options.StreamKey, StatusKey(state.JobId)],
            [JsonSerializer.Serialize(state, _jsonOptions), (long)_options.StateTtl.TotalMilliseconds,
             state.JobId, state.Status]);
        return result.Resp2Type != ResultType.Integer || (long)result != 0;
    }

    public async Task<DurableJobState?> GetAsync(string jobId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var value = await Database.StringGetAsync(StateKey(jobId));
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<DurableJobState>(value!, _jsonOptions);
    }

    public async Task<DurableJobMessage?> ReadNextAsync(string consumer, CancellationToken ct)
    {
        await EnsureConsumerGroupAsync(ct);
        var reclaimed = await Database.StreamAutoClaimAsync(
            _options.StreamKey, _options.ConsumerGroup, consumer,
            (long)_options.VisibilityTimeout.TotalMilliseconds, "0-0", 1);
        if (reclaimed.ClaimedEntries.Length > 0)
            return ToMessage(reclaimed.ClaimedEntries[0]);

        var entries = await Database.StreamReadGroupAsync(
            _options.StreamKey, _options.ConsumerGroup, consumer, ">", 1);
        return entries.Length == 0 ? null : ToMessage(entries[0]);
    }

    public async Task<bool> TryClaimAsync(DurableJobMessage message, string worker, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = await GetAsync(message.JobId, ct);
        if (state is null || state.Status is DurableJobStatus.Completed or DurableJobStatus.DeadLetter)
            return false;

        var result = await Database.ScriptEvaluateAsync(
            "if redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2], 'NX') then " +
            "redis.call('SET', KEYS[2], ARGV[3], 'PX', ARGV[4]) return 1 else return 0 end",
            [ClaimKey(message.JobId), StatusKey(message.JobId)],
            [worker, (long)_options.VisibilityTimeout.TotalMilliseconds,
             DurableJobStatus.Running, (long)_options.StateTtl.TotalMilliseconds]);
        if ((int)result != 1) return false;

        state.Status = DurableJobStatus.Running;
        state.Attempt++;
        await SaveAsync(state, ct);
        return true;
    }

    public async Task UpdateProgressAsync(string jobId, int processed, int total, CancellationToken ct)
    {
        var state = await GetAsync(jobId, ct) ?? throw new InvalidOperationException($"Job '{jobId}' was not found.");
        state.Processed = Math.Max(0, processed);
        state.Total = Math.Max(0, total);
        state.Progress = state.Total == 0 ? 0 : Math.Clamp(state.Processed * 100 / state.Total, 0, 100);
        await SaveAsync(state, ct);
    }

    public async Task CompleteAsync(DurableJobMessage message, CancellationToken ct)
    {
        var state = await GetAsync(message.JobId, ct) ?? throw new InvalidOperationException($"Job '{message.JobId}' was not found.");
        state.Status = DurableJobStatus.Completed;
        state.Progress = 100;
        state.Processed = state.Total;
        state.Error = null;
        await SaveAsync(state, ct);
        await Database.KeyDeleteAsync(ClaimKey(message.JobId));
        await AcknowledgeAsync(message.MessageId);
    }

    public async Task RetryAsync(DurableJobMessage message, string error, CancellationToken ct)
    {
        var state = await GetAsync(message.JobId, ct) ?? throw new InvalidOperationException($"Job '{message.JobId}' was not found.");
        state.Error = error;
        if (state.Attempt >= _options.MaxAttempts)
        {
            state.Status = DurableJobStatus.DeadLetter;
            await SaveAsync(state, ct);
            await Database.StreamAddAsync(DeadLetterKey, [new NameValueEntry("jobId", state.JobId), new NameValueEntry("error", error)]);
        }
        else
        {
            state.Status = DurableJobStatus.Queued;
            await SaveAsync(state, ct);
            await Database.StreamAddAsync(_options.StreamKey, [new NameValueEntry("jobId", state.JobId)]);
        }
        await Database.KeyDeleteAsync(ClaimKey(message.JobId));
        await AcknowledgeAsync(message.MessageId);
    }

    public async Task<bool> RedriveAsync(string jobId, CancellationToken ct)
    {
        var state = await GetAsync(jobId, ct);
        if (state is null || state.Status != DurableJobStatus.DeadLetter) return false;
        state.Status = DurableJobStatus.Queued;
        state.Attempt = 0;
        state.Error = null;
        await SaveAsync(state, ct);
        await Database.StreamAddAsync(_options.StreamKey, [new NameValueEntry("jobId", jobId)]);
        return true;
    }

    public Task AcknowledgeAsync(RedisValue messageId) =>
        Database.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, messageId);

    private async Task SaveAsync(DurableJobState state, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        state.UpdatedAt = DateTimeOffset.UtcNow;
        await Database.StringSetAsync(StateKey(state.JobId), JsonSerializer.Serialize(state, _jsonOptions), _options.StateTtl);
        await Database.StringSetAsync(StatusKey(state.JobId), state.Status, _options.StateTtl);
    }

    private DurableJobMessage ToMessage(StreamEntry entry) =>
        new(entry.Values.FirstOrDefault(v => v.Name == "jobId").Value!, entry.Id);
    private IDatabase Database => _redis.GetDatabase();
    private string StateKey(string jobId) => $"{_options.StreamKey}:state:{jobId}";
    private string StatusKey(string jobId) => $"{_options.StreamKey}:status:{jobId}";
    private string ClaimKey(string jobId) => $"{_options.StreamKey}:claim:{jobId}";
    private string DeadLetterKey => $"{_options.StreamKey}:dead-letter";
}

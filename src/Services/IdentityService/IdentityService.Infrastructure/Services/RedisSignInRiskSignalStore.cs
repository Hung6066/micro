using System.Globalization;
using His.Hope.IdentityService.Application.Risk;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>
/// Redis-backed sign-in history for the risk score (Zero Trust 3.5).
/// IP prefixes live in a sorted set scored by last-seen time, so each prefix ages out
/// after <see cref="RiskScoringOptions.HistoryDays"/>; the hour histogram is a hash whose
/// key expires after the same horizon without a sign-in. No raw IP address is stored.
/// </summary>
public sealed class RedisSignInRiskSignalStore(IConnectionMultiplexer redis, IOptions<RiskScoringOptions> options, TimeProvider clock)
    : ISignInRiskSignalStore
{
    private TimeSpan History => TimeSpan.FromDays(Math.Max(1, options.Value.HistoryDays));

    public async Task<SignInRiskHistory> GetHistoryAsync(Guid userId, string? ipPrefix, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var prefixKey = RiskSignalKeys.IpPrefixes(userId);
        var cutoff = clock.GetUtcNow().Add(-History).ToUnixTimeSeconds();
        var baselineTask = db.SortedSetLengthAsync(prefixKey, cutoff, double.PositiveInfinity);
        var seenTask = ipPrefix is null ? Task.FromResult<double?>(null) : db.SortedSetScoreAsync(prefixKey, ipPrefix);
        var hoursTask = db.HashGetAllAsync(RiskSignalKeys.SignInHours(userId));
        await Task.WhenAll(baselineTask, seenTask, hoursTask);

        var histogram = new Dictionary<int, long>();
        foreach (var entry in hoursTask.Result)
        {
            if (int.TryParse(entry.Name.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour) &&
                hour is >= 0 and <= 23 && entry.Value.TryParse(out long count) && count > 0)
                histogram[hour] = count;
        }

        var seen = seenTask.Result;
        return new SignInRiskHistory(
            HasPrefixBaseline: baselineTask.Result > 0,
            KnownPrefix: seen is { } lastSeen && lastSeen >= cutoff,
            HourHistogram: histogram);
    }

    public async Task RecordSuccessAsync(Guid userId, string? ipPrefix, int localHour, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var now = clock.GetUtcNow();
        var prefixKey = RiskSignalKeys.IpPrefixes(userId);
        var hoursKey = RiskSignalKeys.SignInHours(userId);
        var batch = db.CreateBatch();
        var tasks = new List<Task>();
        if (ipPrefix is not null)
        {
            tasks.Add(batch.SortedSetAddAsync(prefixKey, ipPrefix, now.ToUnixTimeSeconds()));
            tasks.Add(batch.SortedSetRemoveRangeByScoreAsync(prefixKey, double.NegativeInfinity, now.Add(-History).ToUnixTimeSeconds(), Exclude.Stop));
            tasks.Add(batch.KeyExpireAsync(prefixKey, History));
        }
        tasks.Add(batch.HashIncrementAsync(hoursKey, localHour.ToString(CultureInfo.InvariantCulture)));
        tasks.Add(batch.KeyExpireAsync(hoursKey, History));
        batch.Execute();
        await Task.WhenAll(tasks);
    }

    public Task<bool> HasRecentRefreshTokenReuseAsync(Guid userId, CancellationToken cancellationToken = default) =>
        redis.GetDatabase().KeyExistsAsync(RiskSignalKeys.RefreshTokenReuse(userId));

    public Task RememberPendingPrimaryAsync(string pendingId, int failedPasswordAttempts, TimeSpan lifetime, CancellationToken cancellationToken = default) =>
        redis.GetDatabase().StringSetAsync(RiskSignalKeys.PendingPrimary(pendingId), failedPasswordAttempts, lifetime);

    public async Task<int> TakePendingPrimaryAsync(string pendingId, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var key = RiskSignalKeys.PendingPrimary(pendingId);
        var value = await db.StringGetAsync(key);
        if (value.HasValue) await db.KeyDeleteAsync(key);
        return value.TryParse(out int count) ? count : 0;
    }

    /// <summary>Marks a refresh-token reuse for the user so the next sign-in scores it.</summary>
    public static Task MarkRefreshTokenReuseAsync(IDatabase db, string? userId, TimeSpan window) =>
        Guid.TryParse(userId, out var id)
            ? db.StringSetAsync(RiskSignalKeys.RefreshTokenReuse(id), "1", window)
            : Task.CompletedTask;
}

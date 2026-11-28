using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Services;

/// <summary>
/// Second-factor brute-force and replay protection. The "mfa" rate limiter is
/// keyed by network address only; this guard counts failures per account and
/// makes every accepted TOTP time step single-use (RFC 6238 section 5.2).
/// </summary>
public sealed class MfaAttemptGuard(IConnectionMultiplexer redis)
{
    public const int MaxFailedAttempts = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
    // Covers the accepted drift window (previous, current and next 30 s step).
    private static readonly TimeSpan TotpStepRetention = TimeSpan.FromMinutes(2);

    private static string FailureKey(Guid userId) => $"hishop:identity:mfa-failures:{userId:N}";
    private static string TotpStepKey(Guid userId, long step) => $"hishop:identity:totp-step:{userId:N}:{step}";

    public async Task<bool> IsBlockedAsync(Guid userId)
    {
        var value = await redis.GetDatabase().StringGetAsync(FailureKey(userId));
        return value.HasValue && (long)value >= MaxFailedAttempts;
    }

    /// <summary>Failed second-factor attempts in the current 15-minute window (risk signal).</summary>
    public async Task<int> GetFailureCountAsync(Guid userId)
    {
        var value = await redis.GetDatabase().StringGetAsync(FailureKey(userId));
        return value.HasValue && value.TryParse(out int count) ? count : 0;
    }

    public async Task RecordFailureAsync(Guid userId)
    {
        var db = redis.GetDatabase();
        var key = FailureKey(userId);
        if (await db.StringIncrementAsync(key) == 1)
            await db.KeyExpireAsync(key, FailureWindow);
    }

    public Task ResetAsync(Guid userId) => redis.GetDatabase().KeyDeleteAsync(FailureKey(userId));

    /// <returns>false when this time step was already accepted for the user.</returns>
    public Task<bool> TryConsumeTotpStepAsync(Guid userId, long step) =>
        redis.GetDatabase().StringSetAsync(TotpStepKey(userId, step), "1", TotpStepRetention, When.NotExists);
}

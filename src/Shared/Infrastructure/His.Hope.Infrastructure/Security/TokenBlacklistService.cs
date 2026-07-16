using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace His.Hope.Infrastructure.Security;

/// <summary>
/// Token blacklist service for JWT revocation.
/// Stores revoked token jti (JWT ID) claims in Redis with TTL matching token expiry.
/// 
/// HIPAA Context:
///   164.312(a)(1) Access Control: Immediate token revocation prevents unauthorized access
///   164.312(c)(1) Integrity Controls: Ensures revoked tokens cannot be reused
///   164.312(d) Person or Entity Authentication: Maintains authentication integrity
/// </summary>
public interface ITokenBlacklistService
{
    Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct = default);
    Task<bool> IsBlacklistedAsync(string jti, CancellationToken ct = default);
    Task RevokeAllUserTokensAsync(string userId, CancellationToken ct = default);
    Task<DateTime?> GetUserRevocationTimestampAsync(string userId, CancellationToken ct = default);
}

public sealed class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<TokenBlacklistService> _logger;

    private const string BlacklistKeyPrefix = "HisHope:token_blacklist:";
    private const string UserRevocationPrefix = "HisHope:user_revocation:";

    public TokenBlacklistService(
        IDistributedCache cache,
        ILogger<TokenBlacklistService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
            throw new ArgumentException("jti cannot be null or empty", nameof(jti));

        var key = BuildBlacklistKey(jti);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };

        var entry = new TokenBlacklistEntry
        {
            RevokedAt = DateTime.UtcNow,
            Jti = jti
        };

        var json = JsonSerializer.Serialize(entry);
        await _cache.SetStringAsync(key, json, options, ct);

        _logger.LogInformation(
            "Token blacklisted: jti={Jti}, ttl={Ttl}s", jti, ttl.TotalSeconds);
    }

    public async Task<bool> IsBlacklistedAsync(string jti, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
            return false;

        var key = BuildBlacklistKey(jti);
        var result = await _cache.GetStringAsync(key, ct);
        return result is not null;
    }

    public async Task RevokeAllUserTokensAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("userId cannot be null or empty", nameof(userId));

        var key = BuildUserRevocationKey(userId);
        var revokedAt = DateTime.UtcNow;

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
        };

        await _cache.SetStringAsync(key, revokedAt.ToString("O"), options, ct);

        _logger.LogWarning(
            "All tokens revoked for user: UserId={UserId}, RevokedAt={RevokedAt}",
            userId, revokedAt);
    }

    public async Task<DateTime?> GetUserRevocationTimestampAsync(string userId, CancellationToken ct = default)
    {
        var key = BuildUserRevocationKey(userId);
        var value = await _cache.GetStringAsync(key, ct);

        if (value is null) return null;
        if (DateTime.TryParse(value, out var timestamp))
            return timestamp;
        return null;
    }

    private static string BuildBlacklistKey(string jti) => BlacklistKeyPrefix + jti;
    private static string BuildUserRevocationKey(string userId) => UserRevocationPrefix + userId;

    private sealed class TokenBlacklistEntry
    {
        public string Jti { get; init; } = string.Empty;
        public DateTime RevokedAt { get; init; }
    }
}

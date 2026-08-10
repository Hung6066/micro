using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Services;

/// <summary>
/// Binds JWT tokens to (user_id, ip_hash, client_id) in Redis
/// to prevent cross-IP token replay attacks.
/// </summary>
public class TokenBindingService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<TokenBindingService> _logger;

    public TokenBindingService(IConnectionMultiplexer redis, ILogger<TokenBindingService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task BindTokenAsync(string jti, string userId, string ipAddress, string clientId,
        TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var ipHash = ComputeIpHash(ipAddress);
        var value = $"{userId}:{ipHash}:{clientId}";
        await db.StringSetAsync($"token_binding:{jti}", value, ttl ?? TimeSpan.FromHours(1));
    }

    public async Task<bool> ValidateBindingAsync(string jti, string userId, string ipAddress,
        CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var boundData = await db.StringGetAsync($"token_binding:{jti}");
        if (!boundData.HasValue) return true;

        var parts = boundData.ToString().Split(':');
        if (parts.Length < 2) return false;

        var boundUserId = parts[0];
        var boundIpHash = parts[1];
        var currentIpHash = ComputeIpHash(ipAddress);

        return boundUserId == userId && boundIpHash == currentIpHash;
    }

    private static string ComputeIpHash(string ip)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(ip ?? "unknown"));
        return Convert.ToHexString(bytes)[..12];
    }
}

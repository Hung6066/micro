using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public sealed class RedisRefreshTokenStore
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisRefreshTokenStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string TokenPrefix = "HisHope:refresh_token:";
    private const string UserIndexPrefix = "HisHope:user_tokens:";
    private const string FamilyPrefix = "HisHope:token_family:";

    public RedisRefreshTokenStore(
        IDistributedCache cache,
        ILogger<RedisRefreshTokenStore> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task StoreAsync(RefreshTokenRecord record, CancellationToken ct = default)
    {
        var ttl = record.ExpiresAt - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero)
            ttl = TimeSpan.FromMinutes(15);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };

        var tokenKey = BuildTokenKey(record.TokenHash);
        var json = JsonSerializer.Serialize(record, _jsonOptions);
        await _cache.SetStringAsync(tokenKey, json, options, ct);

        _logger.LogDebug(
            "Refresh token stored: UserId={UserId}, FamilyId={FamilyId}, Generation={Gen}",
            record.UserId, record.FamilyId, record.Generation);
    }

    public async Task<RefreshTokenRecord?> GetByTokenAsync(
        string refreshToken, CancellationToken ct = default)
    {
        var hash = RefreshTokenRecord.ComputeHash(refreshToken);
        var key = BuildTokenKey(hash);
        var json = await _cache.GetStringAsync(key, ct);

        if (json is null) return null;

        try
        {
            return JsonSerializer.Deserialize<RefreshTokenRecord>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize refresh token record");
            return null;
        }
    }

    public async Task InvalidateAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = RefreshTokenRecord.ComputeHash(refreshToken);
        var key = BuildTokenKey(hash);
        var json = await _cache.GetStringAsync(key, ct);

        if (json is null) return;

        try
        {
            var record = JsonSerializer.Deserialize<RefreshTokenRecord>(json, _jsonOptions);
            if (record is not null)
            {
                record.IsUsed = true;
                var updatedJson = JsonSerializer.Serialize(record, _jsonOptions);
                await _cache.SetStringAsync(key, updatedJson, ct);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize refresh token for invalidation");
        }
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = RefreshTokenRecord.ComputeHash(refreshToken);
        var key = BuildTokenKey(hash);
        var json = await _cache.GetStringAsync(key, ct);

        if (json is null) return;

        try
        {
            var record = JsonSerializer.Deserialize<RefreshTokenRecord>(json, _jsonOptions);
            if (record is not null)
            {
                record.IsRevoked = true;
                var updatedJson = JsonSerializer.Serialize(record, _jsonOptions);
                await _cache.SetStringAsync(key, updatedJson, ct);

                _logger.LogInformation(
                    "Refresh token revoked: UserId={UserId}, FamilyId={FamilyId}",
                    record.UserId, record.FamilyId);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize refresh token for revocation");
        }
    }

    public async Task<(bool WasReused, string? FamilyId)> DetectReuseAsync(
        string refreshToken, CancellationToken ct = default)
    {
        var record = await GetByTokenAsync(refreshToken, ct);

        if (record is null)
            return (false, null);

        if (record.IsUsed)
        {
            _logger.LogWarning(
                "Refresh token reuse detected! FamilyId={FamilyId}, UserId={UserId}, " +
                "Generation={Generation}, Device={Device}, IP={IP}",
                record.FamilyId, record.UserId, record.Generation,
                record.DeviceInfo, record.IpAddress);

            await RevokeFamilyAsync(record.FamilyId, ct);
            return (true, record.FamilyId);
        }

        return (false, record.FamilyId);
    }

    public async Task RevokeFamilyAsync(string familyId, CancellationToken ct = default)
    {
        var familyKey = BuildFamilyKey(familyId);
        var revocationMarker = new
        {
            RevokedAt = DateTime.UtcNow,
            FamilyId = familyId,
            Reason = "token_reuse_detected"
        };

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
        };

        var json = JsonSerializer.Serialize(revocationMarker, _jsonOptions);
        await _cache.SetStringAsync(familyKey, json, options, ct);

        _logger.LogWarning(
            "Entire token family revoked: FamilyId={FamilyId}", familyId);
    }

    public async Task<bool> IsFamilyRevokedAsync(string familyId, CancellationToken ct = default)
    {
        var familyKey = BuildFamilyKey(familyId);
        var result = await _cache.GetStringAsync(familyKey, ct);
        return result is not null;
    }

    public static string GenerateFamilyId() => Guid.NewGuid().ToString("N");

    private static string BuildTokenKey(string tokenHash) => TokenPrefix + tokenHash;
    private static string BuildFamilyKey(string familyId) => FamilyPrefix + familyId;
}

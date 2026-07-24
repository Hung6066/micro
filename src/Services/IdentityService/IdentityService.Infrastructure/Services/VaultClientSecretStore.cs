using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class VaultClientSecretStore
{
    private readonly IConfiguration _config;
    private readonly ILogger<VaultClientSecretStore> _logger;
    private readonly ConcurrentDictionary<string, CachedSecret> _cache = new();

    public VaultClientSecretStore(IConfiguration config, ILogger<VaultClientSecretStore> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string GenerateSecret(string clientId)
    {
        var bytes = new byte[36];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var secret = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        _cache[clientId] = new CachedSecret(secret, DateTime.UtcNow.AddMinutes(5));

        _logger.LogInformation("Generated new client secret for {ClientId}", clientId);
        return secret;
    }

    public Task<string?> GetSecretAsync(string clientId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(clientId, out var cached) && !cached.IsExpired)
        {
            return Task.FromResult<string?>(cached.Value);
        }

        _logger.LogDebug("Client secret cache miss for {ClientId}", clientId);
        return Task.FromResult<string?>(null);
    }

    public Task StoreSecretAsync(string clientId, string secret, CancellationToken ct = default)
    {
        _cache[clientId] = new CachedSecret(secret, DateTime.UtcNow.AddMinutes(5));

        _logger.LogInformation("Stored client secret for {ClientId}", clientId);
        return Task.CompletedTask;
    }

    public async Task<bool> ValidateSecretAsync(string clientId, string secret, CancellationToken ct = default)
    {
        var stored = await GetSecretAsync(clientId, ct);
        if (stored is null) return false;

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(secret),
            System.Text.Encoding.UTF8.GetBytes(stored));
    }

    public Task RevokeSecretAsync(string clientId, CancellationToken ct = default)
    {
        _cache.TryRemove(clientId, out _);

        _logger.LogInformation("Revoked client secret for {ClientId}", clientId);
        return Task.CompletedTask;
    }

    private record CachedSecret(string Value, DateTime ExpiresAt)
    {
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}

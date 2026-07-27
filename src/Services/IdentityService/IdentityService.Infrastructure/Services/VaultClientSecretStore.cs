using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class VaultClientSecretStore
{
    private readonly IConfiguration _config;
    private readonly ILogger<VaultClientSecretStore> _logger;
    private readonly ConcurrentDictionary<string, CachedSecret> _cache = new();
    private readonly HttpClient? _vaultClient;
    private readonly string _vaultPathPrefix;
    private readonly string _vaultSecretsMount;

    public VaultClientSecretStore(IConfiguration config, ILogger<VaultClientSecretStore> logger, IHostEnvironment environment)
    {
        _config = config;
        _logger = logger;
        _vaultSecretsMount = config["Vault:SecretsMount"] ?? "secret";
        _vaultPathPrefix = config["Vault:SecretsPathPrefix"] ?? "his-hope/identity/client-secrets";
        var vaultAddress = config["Vault:Address"];
        var vaultToken = FirstConfigured(config["Vault:Token"], config["VAULT_TOKEN"]);
        var requireVault = config.GetValue("Vault:RequireVault", environment.IsProduction());
        if (requireVault && string.IsNullOrWhiteSpace(vaultAddress))
            throw new InvalidOperationException("Vault:Address is required when Vault:RequireVault is enabled.");
        if (requireVault && string.IsNullOrWhiteSpace(vaultToken))
            throw new InvalidOperationException("Vault token is required when Vault:RequireVault is enabled.");
        if (!string.IsNullOrWhiteSpace(vaultAddress) && !string.IsNullOrWhiteSpace(vaultToken))
        {
            _vaultClient = new HttpClient { BaseAddress = new Uri(vaultAddress.TrimEnd('/')), Timeout = TimeSpan.FromSeconds(5) };
            _vaultClient.DefaultRequestHeaders.Add("X-Vault-Token", vaultToken);
        }
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

    public async Task<string?> GetSecretAsync(string clientId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(clientId, out var cached) && !cached.IsExpired)
        {
            return cached.Value;
        }

        if (_vaultClient is not null)
        {
            using var response = await _vaultClient.GetAsync(SecretPath(clientId), ct);
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (document.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("data", out var values) && values.TryGetProperty("secret", out var value))
                {
                    var secret = value.GetString();
                    if (!string.IsNullOrWhiteSpace(secret))
                    {
                        _cache[clientId] = new CachedSecret(secret, DateTime.UtcNow.AddMinutes(5));
                        return secret;
                    }
                }
            }
        }

        _logger.LogDebug("Client secret cache miss for {ClientId}", clientId);
        return null;
    }

    public async Task StoreSecretAsync(string clientId, string secret, CancellationToken ct = default)
    {
        _cache[clientId] = new CachedSecret(secret, DateTime.UtcNow.AddMinutes(5));
        if (_vaultClient is not null)
        {
            var payload = JsonSerializer.Serialize(new { data = new { secret } });
            using var response = await _vaultClient.PostAsync(SecretPath(clientId), new StringContent(payload, Encoding.UTF8, "application/json"), ct);
            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("Stored client secret for {ClientId}", clientId);
    }

    public async Task<bool> ValidateSecretAsync(string clientId, string secret, CancellationToken ct = default)
    {
        var stored = await GetSecretAsync(clientId, ct);
        if (stored is null) return false;

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(secret),
            System.Text.Encoding.UTF8.GetBytes(stored));
    }

    public async Task RevokeSecretAsync(string clientId, CancellationToken ct = default)
    {
        _cache.TryRemove(clientId, out _);
        if (_vaultClient is not null)
        {
            using var response = await _vaultClient.DeleteAsync(SecretPath(clientId), ct);
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
                response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("Revoked client secret for {ClientId}", clientId);
    }

    private record CachedSecret(string Value, DateTime ExpiresAt)
    {
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    private string SecretPath(string clientId) => $"/v1/{_vaultSecretsMount}/data/{_vaultPathPrefix}/{Uri.EscapeDataString(clientId)}";

    private static string? FirstConfigured(params string?[] values) => values.FirstOrDefault(value =>
        !string.IsNullOrWhiteSpace(value) && !(value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}')));
}

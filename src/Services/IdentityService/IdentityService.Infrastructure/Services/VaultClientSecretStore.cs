using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using His.Hope.Secrets;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class VaultClientSecretStore
{
    private readonly IConfiguration _config;
    private readonly ILogger<VaultClientSecretStore> _logger;
    private readonly ConcurrentDictionary<string, CachedSecret> _cache = new();
    private readonly HttpClient? _vaultClient;
    private readonly string _vaultPathPrefix;
    private readonly string _vaultSecretsMount;
    private readonly IVaultTokenProvider _tokenProvider;
    private readonly IVaultSecretProvider? _secretProvider;
    private readonly IHostEnvironment _environment;
    private readonly string? _developmentSecretSeed;

    public VaultClientSecretStore(
        IConfiguration config,
        ILogger<VaultClientSecretStore> logger,
        IHostEnvironment environment,
        IVaultTokenProvider tokenProvider,
        IHttpClientFactory httpClientFactory,
        IVaultSecretProvider? secretProvider = null)
    {
        _config = config;
        _logger = logger;
        _tokenProvider = tokenProvider;
        _secretProvider = secretProvider;
        _environment = environment;
        _developmentSecretSeed = config["Vault:DevelopmentSecretSeed"];
        _vaultSecretsMount = config["Vault:SecretsMount"] ?? "secret";
        _vaultPathPrefix = config["Vault:SecretsPathPrefix"] ?? "his-hope/identity/client-secrets";
        var vaultAddress = config["Vault:Address"];
        var requireVault = config.GetValue("Vault:RequireVault", environment.IsProduction());
        if (requireVault && string.IsNullOrWhiteSpace(vaultAddress))
            throw new InvalidOperationException("Vault:Address is required when Vault:RequireVault is enabled.");
        if (!string.IsNullOrWhiteSpace(vaultAddress))
        {
            _vaultClient = httpClientFactory.CreateClient("vault");
            _vaultClient.BaseAddress = new Uri(vaultAddress.TrimEnd('/'));
            _vaultClient.Timeout = TimeSpan.FromSeconds(5);
        }
    }

    public string GenerateSecret(string clientId)
    {
        if (_vaultClient is null && _environment.IsDevelopment() && !string.IsNullOrWhiteSpace(_developmentSecretSeed))
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_developmentSecretSeed));
            var digest = hmac.ComputeHash(Encoding.UTF8.GetBytes(clientId));
            var deterministic = Convert.ToBase64String(digest)
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
            _cache[clientId] = new CachedSecret(deterministic, DateTime.UtcNow.AddMinutes(5));
            _logger.LogWarning("Using deterministic development client secret derivation for {ClientId}; configure Vault/KMS outside Development.", clientId);
            return deterministic;
        }

        var bytes = new byte[36];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var secret = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        _cache[clientId] = new CachedSecret(secret, DateTime.UtcNow.AddMinutes(5));

        _logger.LogInformation("Generated new client secret for {ClientId}", clientId);
        return secret;
    }

    public bool UsesPersistentStore => _vaultClient is not null;

    public async Task<string?> GetSecretAsync(string clientId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(clientId, out var cached) && !cached.IsExpired)
        {
            return cached.Value;
        }

        if (_secretProvider is not null && _vaultClient is not null)
        {
            var secret = await _secretProvider.GetAsync(SecretLogicalPath(clientId), "secret", ct);
            if (!string.IsNullOrWhiteSpace(secret))
            {
                _cache[clientId] = new CachedSecret(secret, DateTime.UtcNow.AddMinutes(5));
                return secret;
            }
        }
        else if (_vaultClient is not null)
        {
            await AuthenticateAsync(ct);
            using var response = await _vaultClient.GetAsync(SecretPath(clientId), ct);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    try
                    {
                        using var document = JsonDocument.Parse(content);
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
                    catch (JsonException)
                    {
                        _logger.LogWarning("Vault returned an invalid secret payload for {ClientId}", clientId);
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
        if (_secretProvider is not null && _vaultClient is not null)
        {
            await _secretProvider.PutAsync(SecretLogicalPath(clientId), "secret", secret, ct);
        }
        else if (_vaultClient is not null)
        {
            await AuthenticateAsync(ct);
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
        if (_secretProvider is not null && _vaultClient is not null)
        {
            await _secretProvider.DeleteAsync(SecretLogicalPath(clientId), ct);
        }
        else if (_vaultClient is not null)
        {
            await AuthenticateAsync(ct);
            using var response = await _vaultClient.DeleteAsync(SecretPath(clientId), ct);
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
                response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("Revoked client secret for {ClientId}", clientId);
    }

    private async Task AuthenticateAsync(CancellationToken ct)
    {
        _vaultClient!.DefaultRequestHeaders.Remove("X-Vault-Token");
        _vaultClient.DefaultRequestHeaders.Add("X-Vault-Token", await _tokenProvider.GetTokenAsync(ct));
    }

    private sealed record CachedSecret(string Value, DateTime ExpiresAt)
    {
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    private string SecretPath(string clientId) => $"/v1/{_vaultSecretsMount}/data/{_vaultPathPrefix}/{Uri.EscapeDataString(clientId)}";

    private string SecretLogicalPath(string clientId) => $"{_vaultPathPrefix}/{clientId}";

    private static string? FirstConfigured(params string?[] values) => values.FirstOrDefault(value =>
        !string.IsNullOrWhiteSpace(value) && !(value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}')));
}

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Infrastructure.Services;

public partial class VaultKeyService : IVaultKeyProvider, IDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<VaultKeyService> _logger;
    private readonly string _keyName;
    private readonly string? _signingPath;
    private readonly bool _production;
    private readonly bool _useVault;
    private readonly ConcurrentDictionary<string, (RSA Rsa, RsaSecurityKey Key, DateTimeOffset RetireAt)> _activeKeys = new();
    private string _currentKeyId = string.Empty;
    private int _keyVersion;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    private const int OverlapMinutes = 120;
    private const string KeyIdFormat = "{0}:{1}:v{2}";

    private readonly IVaultTokenProvider _tokenProvider;
    private readonly IHttpClientFactory _httpClientFactory;

    public VaultKeyService(
        IConfiguration config,
        ILogger<VaultKeyService> logger,
        IHostEnvironment environment,
        IVaultTokenProvider tokenProvider,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _production = environment.IsProduction();
        _tokenProvider = tokenProvider;
        _httpClientFactory = httpClientFactory;
        _keyName = config["Vault:Transit:KeyName"] ?? "jwt-signing";

        var vaultAddr = config["Vault:Address"];
        _useVault = config.GetValue("Vault:EnableTransit", environment.IsProduction());

        var signingPath = FirstConfigured(
            config["OpenIddict:Signing:PrivateKeyPath"],
            config["Jwt:RsaPrivateKeyPath"]);
        _signingPath = signingPath;
        if (_production && (string.IsNullOrWhiteSpace(signingPath) || !File.Exists(signingPath)))
            throw new InvalidOperationException("Production signing requires a persistent RSA key supplied by Vault Agent/KMS at OpenIddict:Signing:PrivateKeyPath.");

        var version = Interlocked.Increment(ref _keyVersion);
        // The public JWKS endpoint must expose the same key id as OpenIddict.
        // Keeping a second, synthetic id here makes clients receive a key set
        // that cannot validate the access tokens issued by the server.
        var keyId = config["OpenIddict:Signing:KeyId"] ?? "jwt-signing";
        var rsa = RSA.Create();
        if (!string.IsNullOrWhiteSpace(signingPath) && File.Exists(signingPath))
            rsa.ImportFromPem(File.ReadAllText(signingPath));
        else
            rsa.KeySize = 2048;
        var key = new RsaSecurityKey(rsa) { KeyId = keyId };

        _activeKeys[keyId] = (rsa, key, DateTimeOffset.MaxValue);
        _currentKeyId = keyId;

        _cleanupTimer = new Timer(_ => CleanupExpiredKeys(), null, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));

        if (_useVault)
        {
            LogVaultConfigured(_logger, _keyName, vaultAddr);
        }
        else
        {
            LogDevelopmentKey(_logger, keyId);
        }
    }

    public Task<SecurityKey> GetSigningKeyAsync(CancellationToken ct = default)
    {
        if (_activeKeys.TryGetValue(_currentKeyId, out var entry))
            return Task.FromResult<SecurityKey>(entry.Key);
        throw new InvalidOperationException("No active signing key available.");
    }

    public Task<IEnumerable<JsonWebKey>> GetJwksAsync(CancellationToken ct = default)
    {
        var jwks = new List<JsonWebKey>();
        foreach (var kvp in _activeKeys)
        {
            var parameters = kvp.Value.Rsa.ExportParameters(false);
            jwks.Add(new JsonWebKey
            {
                Kty = JsonWebAlgorithmsKeyTypes.RSA,
                Alg = SecurityAlgorithms.RsaSha256,
                Use = "sig",
                Kid = kvp.Key,
                N = Base64UrlEncoder.Encode(parameters.Modulus!),
                E = Base64UrlEncoder.Encode(parameters.Exponent!)
            });
        }
        return Task.FromResult<IEnumerable<JsonWebKey>>(jwks);
    }

    public Task<string> SignAsync(byte[] data, CancellationToken ct = default)
    {
        if (!_activeKeys.TryGetValue(_currentKeyId, out var entry))
            throw new InvalidOperationException("No active signing key available.");

        var signature = entry.Rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Task.FromResult(Convert.ToBase64String(signature));
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        if (_useVault)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("vault-health");
                var vaultAddr = _config["Vault:Address"]!;
                // Vault returns 429 for a healthy standby node. The service endpoint
                // load-balances across the HA set, so standbyok keeps readiness from
                // flapping while still returning non-success for sealed/uninitialized Vault.
                var response = await httpClient.GetAsync($"{vaultAddr}/v1/sys/health?standbyok=true&sealedcode=503&uninitcode=503", ct);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                LogVaultHealthFailed(_logger, ex);
                return false;
            }
        }

        return _activeKeys.Count > 0;
    }

    public async Task RotateKeyAsync(CancellationToken ct = default)
    {
        var prefix = _useVault ? "vault" : "dev";
        var newVersion = Interlocked.Increment(ref _keyVersion);
        var newKeyId = string.Format(KeyIdFormat, prefix, _keyName, newVersion);

        if (_production)
        {
            if (string.IsNullOrWhiteSpace(_signingPath) || !File.Exists(_signingPath))
                throw new InvalidOperationException("Production signing rotation requires the Vault Agent/KMS key file to be present.");

            var rotatedKey = RSA.Create();
            rotatedKey.ImportFromPem(File.ReadAllText(_signingPath));
            ActivateRotatedKey(rotatedKey, newKeyId);
            LogSigningKeyReloaded(_logger, _signingPath);
            return;
        }

        var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = newKeyId };

        if (!string.IsNullOrEmpty(_currentKeyId) && _activeKeys.TryGetValue(_currentKeyId, out var oldEntry))
        {
            _activeKeys[_currentKeyId] = (oldEntry.Rsa, oldEntry.Key, DateTimeOffset.UtcNow.AddMinutes(OverlapMinutes));
            LogKeyRetired(_logger, _currentKeyId, OverlapMinutes);
        }

        _activeKeys[newKeyId] = (rsa, key, DateTimeOffset.MaxValue);
        _currentKeyId = newKeyId;

        LogSigningKeyActivated(_logger, newKeyId);

        if (_useVault)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("vault-health");
                httpClient.DefaultRequestHeaders.Add("X-Vault-Token", await _tokenProvider.GetTokenAsync(ct));
                var vaultAddr = _config["Vault:Address"]!;
                var content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new { }),
                    System.Text.Encoding.UTF8, "application/json");

                await httpClient.PostAsync(
                    $"{vaultAddr}/v1/transit/keys/{_keyName}/rotate", content, ct);
                LogVaultRotationTriggered(_logger, _keyName);
            }
            catch (Exception ex)
            {
                LogVaultRotationFailed(_logger, ex, _keyName);
                throw;
            }
        }
    }

    private void ActivateRotatedKey(RSA rsa, string keyId)
    {
        var key = new RsaSecurityKey(rsa) { KeyId = keyId };
        if (!string.IsNullOrEmpty(_currentKeyId) && _activeKeys.TryGetValue(_currentKeyId, out var oldEntry))
            _activeKeys[_currentKeyId] = (oldEntry.Rsa, oldEntry.Key, DateTimeOffset.UtcNow.AddMinutes(OverlapMinutes));
        _activeKeys[keyId] = (rsa, key, DateTimeOffset.MaxValue);
        _currentKeyId = keyId;
    }

    public void CleanupExpiredKeys()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _activeKeys)
        {
            if (kvp.Key == _currentKeyId)
                continue;

            if (kvp.Value.RetireAt <= now && _activeKeys.TryRemove(kvp.Key, out var entry))
            {
                entry.Rsa.Dispose();
                LogExpiredKeyCleaned(_logger, kvp.Key);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cleanupTimer?.Dispose();
        foreach (var kvp in _activeKeys)
        {
            kvp.Value.Rsa.Dispose();
        }
        _activeKeys.Clear();
    }

    private static string? FirstConfigured(params string?[] values) => values.FirstOrDefault(value =>
        !string.IsNullOrWhiteSpace(value) && !(value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}')));

    [LoggerMessage(EventId = 4101, Level = LogLevel.Information,
        Message = "VaultKeyService: Vault-backed persistent signing key configured for '{KeyName}' at {Address}")]
    private static partial void LogVaultConfigured(ILogger logger, string keyName, string? address);

    [LoggerMessage(EventId = 4102, Level = LogLevel.Information,
        Message = "VaultKeyService: Development mode — ephemeral RSA-2048 key (KeyId: {KeyId})")]
    private static partial void LogDevelopmentKey(ILogger logger, string keyId);

    [LoggerMessage(EventId = 4103, Level = LogLevel.Error, Message = "Vault health check failed")]
    private static partial void LogVaultHealthFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4104, Level = LogLevel.Information,
        Message = "Signing key reloaded from the persistent Vault Agent/KMS path {Path}")]
    private static partial void LogSigningKeyReloaded(ILogger logger, string path);

    [LoggerMessage(EventId = 4105, Level = LogLevel.Information,
        Message = "Key {OldId} retired, will be removed after {Minutes}min overlap")]
    private static partial void LogKeyRetired(ILogger logger, string oldId, int minutes);

    [LoggerMessage(EventId = 4106, Level = LogLevel.Information,
        Message = "New signing key activated: {KeyId}")]
    private static partial void LogSigningKeyActivated(ILogger logger, string keyId);

    [LoggerMessage(EventId = 4107, Level = LogLevel.Information,
        Message = "Vault key rotation triggered for {KeyName}")]
    private static partial void LogVaultRotationTriggered(ILogger logger, string keyName);

    [LoggerMessage(EventId = 4108, Level = LogLevel.Error,
        Message = "Vault key rotation failed for {KeyName}")]
    private static partial void LogVaultRotationFailed(ILogger logger, Exception exception, string keyName);

    [LoggerMessage(EventId = 4109, Level = LogLevel.Information,
        Message = "Cleaned up expired key: {KeyId}")]
    private static partial void LogExpiredKeyCleaned(ILogger logger, string keyId);
}

public class VaultHealthCheck : IHealthCheck
{
    private readonly IVaultKeyProvider _vaultKeyProvider;
    private readonly ILogger<VaultHealthCheck> _logger;

    public VaultHealthCheck(IVaultKeyProvider vaultKeyProvider, ILogger<VaultHealthCheck> logger)
    {
        _vaultKeyProvider = vaultKeyProvider;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        var isHealthy = await _vaultKeyProvider.IsHealthyAsync(ct);
        if (isHealthy)
            return HealthCheckResult.Healthy("Signing key available");
        else
            return HealthCheckResult.Unhealthy("Signing key unavailable. Token issuance will fail.");
    }

}

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

public class VaultKeyService : IVaultKeyProvider, IDisposable
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

    public VaultKeyService(IConfiguration config, ILogger<VaultKeyService> logger, IHostEnvironment environment, IVaultTokenProvider tokenProvider)
    {
        _config = config;
        _logger = logger;
        _production = environment.IsProduction();
        _tokenProvider = tokenProvider;
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
            _logger.LogInformation("VaultKeyService: Vault-backed persistent signing key configured for '{KeyName}' at {Address}",
                _keyName, vaultAddr);
        }
        else
        {
            _logger.LogInformation("VaultKeyService: Development mode — ephemeral RSA-2048 key (KeyId: {KeyId})", keyId);
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
                using var handler = new HttpClientHandler();
                var caPath = _config["Vault:TlsCaFile"];
                if (!string.IsNullOrWhiteSpace(caPath))
                {
                    if (!File.Exists(caPath))
                        throw new InvalidOperationException($"Vault TLS CA file '{caPath}' is missing.");

                    var ca = new X509Certificate2(caPath);
                    handler.ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                    {
                        if (certificate is null)
                            return false;

                        using var chain = new X509Chain();
                        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                        chain.ChainPolicy.CustomTrustStore.Add(ca);
                        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                        return chain.Build(new X509Certificate2(certificate));
                    };
                }

                using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
                var vaultAddr = _config["Vault:Address"]!;
                // Vault returns 429 for a healthy standby node. The service endpoint
                // load-balances across the HA set, so standbyok keeps readiness from
                // flapping while still returning non-success for sealed/uninitialized Vault.
                var response = await httpClient.GetAsync($"{vaultAddr}/v1/sys/health?standbyok=true&sealedcode=503&uninitcode=503", ct);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vault health check failed");
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
            _logger.LogInformation("Signing key reloaded from the persistent Vault Agent/KMS path {Path}", _signingPath);
            return;
        }

        var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = newKeyId };

        if (!string.IsNullOrEmpty(_currentKeyId) && _activeKeys.TryGetValue(_currentKeyId, out var oldEntry))
        {
            _activeKeys[_currentKeyId] = (oldEntry.Rsa, oldEntry.Key, DateTimeOffset.UtcNow.AddMinutes(OverlapMinutes));
            _logger.LogInformation("Key {OldId} retired, will be removed after {Minutes}min overlap",
                _currentKeyId, OverlapMinutes);
        }

        _activeKeys[newKeyId] = (rsa, key, DateTimeOffset.MaxValue);
        _currentKeyId = newKeyId;

        _logger.LogInformation("New signing key activated: {KeyId}", newKeyId);

        if (_useVault)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("X-Vault-Token", await _tokenProvider.GetTokenAsync(ct));
                var vaultAddr = _config["Vault:Address"]!;
                var content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new { }),
                    System.Text.Encoding.UTF8, "application/json");

                await httpClient.PostAsync(
                    $"{vaultAddr}/v1/transit/keys/{_keyName}/rotate", content, ct);
                _logger.LogInformation("Vault key rotation triggered for {KeyName}", _keyName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vault key rotation failed for {KeyName}", _keyName);
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
                _logger.LogInformation("Cleaned up expired key: {KeyId}", kvp.Key);
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

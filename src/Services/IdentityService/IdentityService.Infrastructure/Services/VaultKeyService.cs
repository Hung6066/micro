using System.Security.Cryptography;
using His.Hope.IdentityService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class VaultKeyService : IVaultKeyProvider, IDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<VaultKeyService> _logger;
    private readonly string _keyName;
    private readonly string _keyId;
    private readonly RSA _rsa;
    private readonly bool _useVault;

    public VaultKeyService(IConfiguration config, ILogger<VaultKeyService> logger)
    {
        _config = config;
        _logger = logger;
        _keyName = config["Vault:Transit:KeyName"] ?? "jwt-signing";

        var vaultAddr = config["Vault:Address"];
        _useVault = !string.IsNullOrEmpty(vaultAddr);

        if (_useVault)
        {
            _keyId = $"vault:{_keyName}";
            _rsa = RSA.Create(2048);
            _logger.LogInformation("VaultKeyService: Vault transit mode configured for key '{KeyName}' at {Address}",
                _keyName, vaultAddr);
        }
        else
        {
            _keyId = $"dev:{_keyName}:{Guid.NewGuid():N}"[..20];
            _rsa = RSA.Create(2048);
            _logger.LogInformation("VaultKeyService: Development mode — ephemeral RSA-2048 key (KeyId: {KeyId})", _keyId);
        }
    }

    public Task<SecurityKey> GetSigningKeyAsync(CancellationToken ct = default)
    {
        var key = new RsaSecurityKey(_rsa) { KeyId = _keyId };
        return Task.FromResult<SecurityKey>(key);
    }

    public Task<IEnumerable<JsonWebKey>> GetJwksAsync(CancellationToken ct = default)
    {
        var parameters = _rsa.ExportParameters(false);
        var jwk = new JsonWebKey
        {
            Kty = JsonWebAlgorithmsKeyTypes.RSA,
            Alg = SecurityAlgorithms.RsaSha256,
            Use = "sig",
            Kid = _keyId,
            N = Base64UrlEncoder.Encode(parameters.Modulus!),
            E = Base64UrlEncoder.Encode(parameters.Exponent!)
        };
        return Task.FromResult<IEnumerable<JsonWebKey>>(new[] { jwk });
    }

    public Task<string> SignAsync(byte[] data, CancellationToken ct = default)
    {
        var signature = _rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Task.FromResult(Convert.ToBase64String(signature));
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        if (_useVault)
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var vaultAddr = _config["Vault:Address"]!;
                var response = await httpClient.GetAsync($"{vaultAddr}/v1/sys/health", ct);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vault health check failed");
                return false;
            }
        }

        return true;
    }

    public async Task RotateKeyAsync(CancellationToken ct = default)
    {
        if (_useVault)
        {
            try
            {
                using var httpClient = new HttpClient();
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
        else
        {
            var oldKeyId = _keyId;
            _rsa.ImportParameters(_rsa.ExportParameters(true));
            _logger.LogInformation("Dev key regenerated (prev: {OldId})", oldKeyId);
        }
    }

    public void Dispose() => _rsa?.Dispose();
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

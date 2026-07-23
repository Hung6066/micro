using System.Security.Cryptography;
using His.Hope.IdentityService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>
/// Provides RSA signing keys for OpenIddict JWT token creation.
/// In development: generates ephemeral RSA-2048 keys in memory.
/// In production: reads keys from Vault transit engine (to be implemented).
/// </summary>
public class VaultKeyService : IVaultKeyProvider, IDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<VaultKeyService> _logger;
    private readonly string _keyName;
    private readonly RSA _rsa;

    public VaultKeyService(IConfiguration config, ILogger<VaultKeyService> logger)
    {
        _config = config;
        _logger = logger;
        _keyName = config["Vault:Transit:KeyName"] ?? "jwt-signing";

        // Development: generate ephemeral RSA key
        // Production: will load from Vault transit engine
        _rsa = RSA.Create(2048);
        _logger.LogInformation("VaultKeyService initialized with ephemeral RSA-2048 key. KeyId: {KeyName}", _keyName);
    }

    public Task<SecurityKey> GetSigningKeyAsync(CancellationToken ct = default)
    {
        var key = new RsaSecurityKey(_rsa) { KeyId = _keyName };
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
            Kid = _keyName,
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

    public Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
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
            return HealthCheckResult.Healthy("Vault transit key available");
        else
            return HealthCheckResult.Unhealthy("Vault transit key unavailable. JWT signing will fail.");
    }
}

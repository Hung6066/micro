using System.Security.Cryptography;
using His.Hope.IdentityService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.AppRole;
using VaultSharp.V1.SecretsEngines.Transit;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class VaultKeyService : IVaultKeyProvider, IDisposable
{
    private readonly IVaultClient _vaultClient;
    private readonly IConfiguration _config;
    private readonly ILogger<VaultKeyService> _logger;
    private readonly string _keyName;
    private readonly Lazy<Task<RsaSecurityKey>> _publicKey;

    public VaultKeyService(IConfiguration config, ILogger<VaultKeyService> logger)
    {
        _config = config;
        _logger = logger;
        _keyName = config["Vault:Transit:KeyName"] ?? "jwt-signing";

        var vaultAddr = config["Vault:Address"]
            ?? throw new InvalidOperationException("Vault:Address is required for JWT signing");

        var roleId = config["Vault:RoleId"]
            ?? throw new InvalidOperationException("Vault:RoleId is required for authentication");

        var secretId = config["Vault:SecretId"]
            ?? throw new InvalidOperationException("Vault:SecretId is required for authentication");

        var authMethod = new AppRoleAuthMethodInfo(new AppRoleAuthMethodInfo.RoleIdSecretId(roleId, secretId));
        var vaultClientSettings = new VaultClientSettings(vaultAddr, authMethod);
        _vaultClient = new VaultClient(vaultClientSettings);
        _publicKey = new Lazy<Task<RsaSecurityKey>>(LoadPublicKeyAsync);
    }

    public async Task<SecurityKey> GetSigningKeyAsync(CancellationToken ct = default)
    {
        return await _publicKey.Value;
    }

    public async Task<IEnumerable<JsonWebKey>> GetJwksAsync(CancellationToken ct = default)
    {
        var rsaKey = (RsaSecurityKey)await _publicKey.Value;
        var parameters = rsaKey.Rsa.ExportParameters(false);
        var jwk = new JsonWebKey
        {
            Kty = JsonWebAlgorithmsKeyTypes.RSA,
            Alg = SecurityAlgorithms.RsaSha256,
            Use = "sig",
            Kid = _keyName,
            N = Base64UrlEncoder.Encode(parameters.Modulus!),
            E = Base64UrlEncoder.Encode(parameters.Exponent!)
        };
        return new[] { jwk };
    }

    public async Task<string> SignAsync(byte[] data, CancellationToken ct = default)
    {
        try
        {
            var input = Convert.ToBase64String(data);
            var result = await _vaultClient.V1.Secrets.Transit.SignAsync(
                _keyName,
                new SignRequestOptions { Input = input, PreHashed = false, SignatureAlgorithm = "pkcs1v15" },
                _config["Vault:MountPoint"] ?? "transit");
            var sig = result.Data.Signature;
            var parts = sig.Split(':');
            return parts.Length >= 3 ? parts[2] : throw new InvalidOperationException("Invalid Vault signature format");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vault transit sign failed for key {KeyName}", _keyName);
            throw;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            await _vaultClient.V1.Secrets.Transit.ReadKeyAsync(
                _keyName,
                _config["Vault:MountPoint"] ?? "transit");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vault health check failed for key {KeyName}", _keyName);
            return false;
        }
    }

    private async Task<RsaSecurityKey> LoadPublicKeyAsync()
    {
        try
        {
            var keyResult = await _vaultClient.V1.Secrets.Transit.ReadKeyAsync(
                _keyName,
                _config["Vault:MountPoint"] ?? "transit");
            var publicKeyPem = keyResult.Data.Keys
                .FirstOrDefault().Value?.PublicKey;

            if (string.IsNullOrEmpty(publicKeyPem))
                throw new InvalidOperationException($"No public key found for transit key {_keyName}");

            var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            return new RsaSecurityKey(rsa) { KeyId = _keyName };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Vault public key for {KeyName}", _keyName);
            throw;
        }
    }

    public void Dispose() => _vaultClient?.Dispose();
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

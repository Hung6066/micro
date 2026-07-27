using System.Text;
using System.Text.Json;
using His.Hope.IdentityService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class VaultMfaSecretEncryptor : IMfaSecretEncryptor, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<VaultMfaSecretEncryptor> _logger;
    private readonly string _keyName;
    private readonly bool _configured;

    public VaultMfaSecretEncryptor(IConfiguration config, ILogger<VaultMfaSecretEncryptor> logger)
    {
        _config = config;
        _logger = logger;
        _keyName = config["Vault:Transit:MfaKeyName"] ?? "mfa-secret";

        var vaultAddr = config["Vault:Address"];
        _configured = !string.IsNullOrEmpty(vaultAddr);

        if (_configured)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(vaultAddr!.TrimEnd('/')),
                Timeout = TimeSpan.FromSeconds(5)
            };
            var vaultToken = FirstConfigured(config["Vault:Token"], config["VAULT_TOKEN"]);
            if (string.IsNullOrWhiteSpace(vaultToken))
                throw new InvalidOperationException("Vault transit requires Vault:Token or VAULT_TOKEN.");
            _httpClient.DefaultRequestHeaders.Add("X-Vault-Token", vaultToken);
            _logger.LogInformation("VaultMfaSecretEncryptor: Vault transit mode for key '{KeyName}'", _keyName);
        }
        else
        {
            _httpClient = null!;
            throw new InvalidOperationException(
                "Vault is not configured. Use AesMfaSecretEncryptor (DataProtection) for development.");
        }
    }

    public string Encrypt(string plaintext)
    {
        if (!_configured)
            throw new InvalidOperationException("Vault is not configured.");

        try
        {
            var plaintextBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));
            var payload = JsonSerializer.Serialize(new { plaintext = plaintextBase64 });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = _httpClient.PostAsync(
                $"/v1/transit/encrypt/{_keyName}", content).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var ciphertext = doc.RootElement.GetProperty("data").GetProperty("ciphertext").GetString();

            if (string.IsNullOrEmpty(ciphertext))
                throw new InvalidOperationException("Vault encrypt response missing ciphertext.");

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(ciphertext));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vault MFA secret encryption failed");
            throw;
        }
    }

    public string Decrypt(string ciphertext)
    {
        if (!_configured)
            throw new InvalidOperationException("Vault is not configured.");

        try
        {
            var vaultCiphertext = Encoding.UTF8.GetString(Convert.FromBase64String(ciphertext));
            var payload = JsonSerializer.Serialize(new { ciphertext = vaultCiphertext });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = _httpClient.PostAsync(
                $"/v1/transit/decrypt/{_keyName}", content).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var plaintextBase64 = doc.RootElement.GetProperty("data").GetProperty("plaintext").GetString();

            if (string.IsNullOrEmpty(plaintextBase64))
                throw new InvalidOperationException("Vault decrypt response missing plaintext.");

            return Encoding.UTF8.GetString(Convert.FromBase64String(plaintextBase64));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vault MFA secret decryption failed");
            throw;
        }
    }

    public void Dispose() => _httpClient?.Dispose();

    private static string? FirstConfigured(params string?[] values) => values.FirstOrDefault(value =>
        !string.IsNullOrWhiteSpace(value) && !(value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}')));
}

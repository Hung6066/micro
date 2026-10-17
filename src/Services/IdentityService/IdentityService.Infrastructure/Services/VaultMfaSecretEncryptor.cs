using System.Text;
using System.Text.Json;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

public class VaultMfaSecretEncryptor : IMfaSecretEncryptor, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<VaultMfaSecretEncryptor> _logger;
    private readonly IVaultTokenProvider _tokenProvider;
    private readonly string _keyName;
    private readonly bool _configured;

    public VaultMfaSecretEncryptor(
        IConfiguration config,
        ILogger<VaultMfaSecretEncryptor> logger,
        IVaultTokenProvider tokenProvider,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _tokenProvider = tokenProvider;
        _keyName = config["Vault:Transit:MfaKeyName"] ?? "mfa-secret";

        var vaultAddr = config["Vault:Address"];
        _configured = !string.IsNullOrEmpty(vaultAddr);

        if (_configured)
        {
            _httpClient = httpClientFactory.CreateClient("vault-transit");
            _httpClient.BaseAddress ??= new Uri(vaultAddr!.TrimEnd('/') + "/");
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
            _httpClient.DefaultRequestHeaders.Remove("X-Vault-Token");
            _httpClient.DefaultRequestHeaders.Add("X-Vault-Token", _tokenProvider.GetTokenAsync().GetAwaiter().GetResult());
            var payload = JsonSerializer.Serialize(new { plaintext = plaintextBase64 });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = _httpClient.PostAsync(
                $"/v1/transit/encrypt/{_keyName}", content).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var ciphertext = ReadRequiredString(doc.RootElement, "ciphertext", "encrypt");

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
            _httpClient.DefaultRequestHeaders.Remove("X-Vault-Token");
            _httpClient.DefaultRequestHeaders.Add("X-Vault-Token", _tokenProvider.GetTokenAsync().GetAwaiter().GetResult());
            var payload = JsonSerializer.Serialize(new { ciphertext = vaultCiphertext });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = _httpClient.PostAsync(
                $"/v1/transit/decrypt/{_keyName}", content).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var plaintextBase64 = ReadRequiredString(doc.RootElement, "plaintext", "decrypt");

            return Encoding.UTF8.GetString(Convert.FromBase64String(plaintextBase64));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vault MFA secret decryption failed");
            throw;
        }
    }

    public void Dispose() { }

    private static string ReadRequiredString(JsonElement root, string propertyName, string operation)
    {
        if (!root.TryGetProperty("data", out var data) ||
            !data.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException($"Vault {operation} response missing {propertyName}.");
        }

        return value.GetString()!;
    }

    private static string? FirstConfigured(params string?[] values) => values.FirstOrDefault(value =>
        !string.IsNullOrWhiteSpace(value) && !(value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}')));
}

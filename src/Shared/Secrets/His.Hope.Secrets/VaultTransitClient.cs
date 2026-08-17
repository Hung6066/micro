using System.Text;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace His.Hope.Secrets;

public sealed record VaultTransitKeyStatus(bool Configured, bool Reachable, bool Sealed, string KeyName, int? KeyVersion, string? Error);

public interface IVaultTransitClient
{
    Task<VaultTransitKeyStatus> GetKeyStatusAsync(string keyName, CancellationToken ct = default);
    Task<string> EncryptAsync(string keyName, ReadOnlyMemory<byte> plaintext, CancellationToken ct = default);
    Task<byte[]> DecryptAsync(string keyName, string ciphertext, CancellationToken ct = default);
}

public sealed class VaultTransitClient(IHttpClientFactory factory, IOptionsMonitor<VaultOptions> options, IVaultTokenProvider tokenProvider, ILogger<VaultTransitClient> logger) : IVaultTransitClient
{
    public async Task<VaultTransitKeyStatus> GetKeyStatusAsync(string keyName, CancellationToken ct = default)
    {
        var client = await CreateClientAsync(ct);
        if (client is null) return new(false, false, false, keyName, null, "Vault is not configured.");
        try
        {
            using var health = await client.GetAsync("/v1/sys/health", ct);
            using var healthDoc = JsonDocument.Parse(await health.Content.ReadAsStringAsync(ct));
            var sealedState = healthDoc.RootElement.TryGetProperty("sealed", out var sealedValue) && sealedValue.GetBoolean();
            if (!health.IsSuccessStatusCode || sealedState) return new(true, false, sealedState, keyName, null, "Vault health is unavailable.");
            using var key = await client.GetAsync($"/v1/{options.CurrentValue.TransitMount}/keys/{Uri.EscapeDataString(keyName)}", ct);
            if (!key.IsSuccessStatusCode) return new(true, true, false, keyName, null, "Transit key is unavailable.");
            using var keyDoc = JsonDocument.Parse(await key.Content.ReadAsStringAsync(ct));
            return new(true, true, false, keyName, keyDoc.RootElement.GetProperty("data").GetProperty("latest_version").GetInt32(), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning("Vault status check failed: {ErrorType}", ex.GetType().Name);
            return new(true, false, false, keyName, null, "Vault status check failed.");
        }
    }

    public async Task<string> EncryptAsync(string keyName, ReadOnlyMemory<byte> plaintext, CancellationToken ct = default)
    {
        var client = await CreateRequiredClientAsync(ct);
        var payload = JsonSerializer.Serialize(new { plaintext = Convert.ToBase64String(plaintext.ToArray()) });
        using var response = await client.PostAsync($"/v1/{options.CurrentValue.TransitMount}/encrypt/{Uri.EscapeDataString(keyName)}", new StringContent(payload, Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("data").GetProperty("ciphertext").GetString()!;
    }

    public async Task<byte[]> DecryptAsync(string keyName, string ciphertext, CancellationToken ct = default)
    {
        var client = await CreateRequiredClientAsync(ct);
        var payload = JsonSerializer.Serialize(new { ciphertext });
        using var response = await client.PostAsync($"/v1/{options.CurrentValue.TransitMount}/decrypt/{Uri.EscapeDataString(keyName)}", new StringContent(payload, Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return Convert.FromBase64String(doc.RootElement.GetProperty("data").GetProperty("plaintext").GetString()!);
    }

    private async Task<HttpClient?> CreateClientAsync(CancellationToken ct)
    {
        var current = options.CurrentValue;
        if (!Uri.TryCreate(current.Address, UriKind.Absolute, out var address)) return null;
        var client = factory.CreateClient("vault");
        client.BaseAddress = address;
        client.DefaultRequestHeaders.Remove("X-Vault-Token");
        client.DefaultRequestHeaders.Add("X-Vault-Token", await tokenProvider.GetTokenAsync(ct));
        return client;
    }

    private async Task<HttpClient> CreateRequiredClientAsync(CancellationToken ct) => await CreateClientAsync(ct) ?? throw new InvalidOperationException("Vault is not configured.");
}

public static class VaultServiceCollectionExtensions
{
    public static IServiceCollection AddHisHopeVault(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<VaultOptions>()
            .Bind(configuration.GetSection(VaultOptions.SectionName))
            .Validate(options => !options.RequireVault ||
                (Uri.TryCreate(options.Address, UriKind.Absolute, out _) &&
                 ((!string.IsNullOrWhiteSpace(options.Token) && options.AllowStaticToken) ||
                  (options.AuthMethod.Equals("approle", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(options.Role) &&
                   (!string.IsNullOrWhiteSpace(options.RoleId) || !string.IsNullOrWhiteSpace(options.RoleIdFile)) &&
                   (!string.IsNullOrWhiteSpace(options.SecretId) || !string.IsNullOrWhiteSpace(options.SecretIdFile))) ||
                  (!options.AuthMethod.Equals("approle", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(options.Role) &&
                   (!string.IsNullOrWhiteSpace(options.JwtTokenFile) || !string.IsNullOrWhiteSpace(options.SpiffeJwtTokenFile))))),
                "Vault is required but Address and workload identity are missing.");
        services.AddHttpClient("vault")
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<VaultOptions>>().CurrentValue;
                var handler = new HttpClientHandler();
                if (!string.IsNullOrWhiteSpace(options.TlsCaFile))
                {
                    if (!File.Exists(options.TlsCaFile))
                        throw new InvalidOperationException($"Vault TLS CA file '{options.TlsCaFile}' is missing.");
                    var ca = new X509Certificate2(options.TlsCaFile);
                    handler.ServerCertificateCustomValidationCallback = (_, certificate, chain, errors) =>
                    {
                        if (certificate is null) return false;
                        using var customChain = new X509Chain();
                        customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                        customChain.ChainPolicy.CustomTrustStore.Add(ca);
                        customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                        return customChain.Build(new X509Certificate2(certificate));
                    };
                }
                return handler;
            });
        services.AddSingleton<IVaultTokenProvider, VaultTokenProvider>();
        services.Configure<VaultDatabaseOptions>(configuration.GetSection(VaultDatabaseOptions.SectionName));
        services.AddSingleton<IVaultDatabaseConnectionStringResolver, VaultDatabaseConnectionStringResolver>();
        services.AddSingleton<IVaultTransitClient, VaultTransitClient>();
        return services;
    }
}

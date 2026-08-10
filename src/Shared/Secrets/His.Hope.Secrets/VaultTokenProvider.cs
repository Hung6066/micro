using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace His.Hope.Secrets;

public interface IVaultTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken ct = default);
    Task InvalidateAsync(CancellationToken ct = default);
}

/// <summary>
/// Exchanges a projected workload JWT for a short-lived Vault token.
/// Static Vault tokens are intentionally rejected in Production.
/// </summary>
public sealed class VaultTokenProvider(
    IHttpClientFactory factory,
    IOptionsMonitor<VaultOptions> options,
    IHostEnvironment environment,
    ILogger<VaultTokenProvider> logger) : IVaultTokenProvider
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? token;
    private DateTimeOffset expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        var current = options.CurrentValue;
        if (!string.IsNullOrWhiteSpace(token) && expiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return token;

        await gate.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(token) && expiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
                return token;

            if (environment.IsProduction() && (!string.IsNullOrWhiteSpace(current.Token) ||
                current.AllowStaticToken || current.AuthMethod.Equals("approle", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Static Vault tokens and AppRole are forbidden in Production; configure Kubernetes or SPIFFE JWT workload identity.");

            if (!string.IsNullOrWhiteSpace(current.Token) && current.AllowStaticToken)
            {
                token = current.Token;
                expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
                return token;
            }

            var client = factory.CreateClient("vault");
            client.BaseAddress = new Uri(current.Address.TrimEnd('/'));
            using var response = await CreateLoginRequestWithRetryAsync(client, current, ct);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var auth = document.RootElement.GetProperty("auth");
            token = auth.GetProperty("client_token").GetString();
            var ttl = auth.TryGetProperty("lease_duration", out var lease)
                ? lease.GetInt32()
                : 300;
            expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, ttl));
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Vault workload identity login returned no token.");
            logger.LogDebug("Vault workload identity token renewed; ttl={TtlSeconds}s", ttl);
            return token;
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<HttpResponseMessage> CreateLoginRequestWithRetryAsync(
        HttpClient client,
        VaultOptions current,
        CancellationToken ct)
    {
        const int maxAttempts = 6;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await CreateLoginRequestAsync(client, current, ct);
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt - 1)));
                await Task.Delay(delay, ct);
            }
        }
    }

    private static async Task<HttpResponseMessage> CreateLoginRequestAsync(
        HttpClient client,
        VaultOptions current,
        CancellationToken ct)
    {
        var method = current.AuthMethod.Trim().ToLowerInvariant();
        if (method is "approle")
        {
            var roleId = await ReadValueAsync(current.RoleId, current.RoleIdFile, ct);
            var secretId = await ReadValueAsync(current.SecretId, current.SecretIdFile, ct);
            if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrWhiteSpace(secretId))
                throw new InvalidOperationException("Vault AppRole requires RoleId and SecretId; use files for non-development secret material.");
            return await client.PostAsJsonAsync(
                $"/v1/auth/{Uri.EscapeDataString(string.IsNullOrWhiteSpace(current.AuthMount) ? "approle" : current.AuthMount.Trim('/'))}/login",
                new { role_id = roleId, secret_id = secretId }, ct);
        }

        if (method is not ("kubernetes" or "jwt" or "spiffe-jwt"))
            throw new InvalidOperationException($"Unsupported Vault auth method '{current.AuthMethod}'. Supported methods: kubernetes, jwt, spiffe-jwt, approle.");
        if (string.IsNullOrWhiteSpace(current.Role))
            throw new InvalidOperationException("Vault JWT authentication requires Role.");
        var tokenFile = method is "spiffe-jwt" && !string.IsNullOrWhiteSpace(current.SpiffeJwtTokenFile)
            ? current.SpiffeJwtTokenFile
            : current.JwtTokenFile;
        if (!File.Exists(tokenFile))
            throw new InvalidOperationException($"Vault JWT workload identity token file '{tokenFile}' is missing.");
        var jwt = await File.ReadAllTextAsync(tokenFile, ct);
        return await client.PostAsJsonAsync(
            $"/v1/auth/{Uri.EscapeDataString(string.IsNullOrWhiteSpace(current.AuthMount) ? (method == "kubernetes" ? "kubernetes" : "jwt") : current.AuthMount.Trim('/'))}/login",
            new { role = current.Role, jwt }, ct);
    }

    private static async Task<string> ReadValueAsync(string value, string file, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(file))
            return File.Exists(file) ? (await File.ReadAllTextAsync(file, ct)).Trim() : string.Empty;
        return value.Trim();
    }

    public Task InvalidateAsync(CancellationToken ct = default)
    {
        token = null;
        expiresAt = DateTimeOffset.MinValue;
        return Task.CompletedTask;
    }
}

using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using His.Hope.IdentityService.Application.Provisioning;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>Google Admin SDK provisioning adapter using a Vault-backed delegated OAuth token.</summary>
public sealed class GoogleWorkspaceProvisioningTarget(IHttpClientFactory clients, VaultClientSecretStore secrets, IConfiguration config) : IProvisioningTarget
{
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTime _tokenExpiresAt;
    public string Name => "google-workspace";
    public async Task<ProvisioningResult> ApplyAsync(ProvisioningChange change, CancellationToken ct = default)
    {
        if (!bool.TryParse(config["Provisioning:GoogleWorkspace:Enabled"], out var enabled) || !enabled)
            return new(false, Error: "Google Workspace outbound provisioning is disabled.");
        var baseUrl = (config["Provisioning:GoogleWorkspace:BaseUrl"] ?? "https://admin.googleapis.com/admin/directory/v1").TrimEnd('/');
        var configuredTokenUrl = config["Provisioning:GoogleWorkspace:TokenUrl"] ?? "https://oauth2.googleapis.com/token";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var googleBase) || googleBase.Scheme != Uri.UriSchemeHttps ||
            !Uri.TryCreate(configuredTokenUrl, UriKind.Absolute, out var googleToken) || googleToken.Scheme != Uri.UriSchemeHttps)
            return new(false, Error: "Google Workspace provisioning endpoints must use HTTPS.");
        var token = await GetAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token)) return new(false, Error: "Google Workspace delegated access token is unavailable from Vault.");
        var resource = change.ResourceType.Equals("Group", StringComparison.OrdinalIgnoreCase) ? "groups" : "users";
        var uri = $"{baseUrl}/{resource}" + (string.IsNullOrWhiteSpace(change.ExternalId) ? "" : "/" + Uri.EscapeDataString(change.ExternalId));
        var method = change.Operation.ToLowerInvariant() switch { "create" => HttpMethod.Post, "delete" => HttpMethod.Delete, _ => HttpMethod.Put };
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (method != HttpMethod.Delete) request.Content = new StringContent(change.Payload.RootElement.GetRawText(), Encoding.UTF8, "application/json");
        using var response = await clients.CreateClient("directory-provisioning").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return new(false, Error: $"Google Admin SDK returned {(int)response.StatusCode}.");
        var externalId = change.ExternalId;
        if (string.IsNullOrWhiteSpace(externalId) && method == HttpMethod.Post)
        {
            try
            {
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                externalId = body.RootElement.TryGetProperty("id", out var id) ? id.GetString() :
                    body.RootElement.TryGetProperty("email", out var email) ? email.GetString() : null;
            }
            catch (JsonException) { }
        }
        return new(true, externalId);
    }

    private async Task<string?> GetAccessTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && _tokenExpiresAt > DateTime.UtcNow.AddMinutes(1))
            return _accessToken;
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) && _tokenExpiresAt > DateTime.UtcNow.AddMinutes(1))
                return _accessToken;
            return await GetAccessTokenCoreAsync(ct);
        }
        finally { _tokenLock.Release(); }
    }

    private async Task<string?> GetAccessTokenCoreAsync(CancellationToken ct)
    {
        var tokenUrl = config["Provisioning:GoogleWorkspace:TokenUrl"] ?? "https://oauth2.googleapis.com/token";
        var serviceAccountJson = await secrets.GetSecretAsync(config["Provisioning:GoogleWorkspace:ServiceAccountSecretId"] ?? "google-workspace-service-account", ct);
        if (string.IsNullOrWhiteSpace(serviceAccountJson)) return null;
        using var account = JsonDocument.Parse(serviceAccountJson);
        var root = account.RootElement;
        var clientEmail = root.TryGetProperty("client_email", out var email) ? email.GetString() : null;
        var privateKeyPem = root.TryGetProperty("private_key", out var key) ? key.GetString() : null;
        var delegatedAdmin = config["Provisioning:GoogleWorkspace:DelegatedAdmin"];
        if (string.IsNullOrWhiteSpace(clientEmail) || string.IsNullOrWhiteSpace(privateKeyPem) || string.IsNullOrWhiteSpace(delegatedAdmin)) return null;
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var now = DateTime.UtcNow;
        var jwt = new JwtSecurityToken(
            issuer: clientEmail,
            audience: tokenUrl,
            claims: new[]
            {
                new System.Security.Claims.Claim("scope", "https://www.googleapis.com/auth/admin.directory.user https://www.googleapis.com/auth/admin.directory.group"),
                new System.Security.Claims.Claim("sub", delegatedAdmin)
            },
            notBefore: now.AddSeconds(-30),
            expires: now.AddMinutes(5),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
        var assertion = new JwtSecurityTokenHandler().WriteToken(jwt);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = assertion
            })
        };
        using var response = await clients.CreateClient("directory-provisioning").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        using var tokenJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        _accessToken = tokenJson.RootElement.TryGetProperty("access_token", out var accessToken) ? accessToken.GetString() : null;
        var expires = tokenJson.RootElement.TryGetProperty("expires_in", out var expiresElement) ? expiresElement.GetInt32() : 3600;
        _tokenExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(60, expires));
        return _accessToken;
    }
}

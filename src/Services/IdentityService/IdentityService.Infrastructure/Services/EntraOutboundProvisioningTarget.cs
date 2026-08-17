using System.Net.Http.Headers;
using System.Text;
using His.Hope.IdentityService.Application.Provisioning;
using Microsoft.Extensions.Configuration;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>Microsoft Graph application-permission provisioning adapter.</summary>
public sealed class EntraOutboundProvisioningTarget(IHttpClientFactory clients, VaultClientSecretStore secrets, IConfiguration config) : IProvisioningTarget
{
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTime _tokenExpiresAt;
    public string Name => "entra";
    public async Task<ProvisioningResult> ApplyAsync(ProvisioningChange change, CancellationToken ct = default)
    {
        if (!bool.TryParse(config["Provisioning:Entra:Enabled"], out var enabled) || !enabled)
            return new(false, Error: "Entra outbound provisioning is disabled.");
        var baseUrl = (config["Provisioning:Entra:BaseUrl"] ?? "https://graph.microsoft.com/v1.0").TrimEnd('/');
        var tokenUrl = config["Provisioning:Entra:TokenUrl"];
        var clientId = config["Provisioning:Entra:ClientId"];
        if (string.IsNullOrWhiteSpace(tokenUrl) || string.IsNullOrWhiteSpace(clientId)) return new(false, Error: "Entra provisioning is not configured.");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var graphUri) || graphUri.Scheme != Uri.UriSchemeHttps ||
            !Uri.TryCreate(tokenUrl, UriKind.Absolute, out var entraToken) || entraToken.Scheme != Uri.UriSchemeHttps)
            return new(false, Error: "Entra provisioning endpoints must use HTTPS.");
        var token = await GetTokenAsync(tokenUrl, clientId, ct);
        if (string.IsNullOrWhiteSpace(token)) return new(false, Error: "Entra token response did not contain access_token.");
        var resource = change.ResourceType.Equals("Group", StringComparison.OrdinalIgnoreCase) ? "groups" : "users";
        var uri = $"{baseUrl}/{resource}" + (string.IsNullOrWhiteSpace(change.ExternalId) ? "" : "/" + Uri.EscapeDataString(change.ExternalId));
        var method = change.Operation.ToLowerInvariant() switch { "create" => HttpMethod.Post, "delete" => HttpMethod.Delete, _ => HttpMethod.Patch };
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (method != HttpMethod.Delete) request.Content = new StringContent(change.Payload.RootElement.GetRawText(), Encoding.UTF8, "application/json");
        using var response = await clients.CreateClient("directory-provisioning").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return new(false, Error: $"Microsoft Graph returned {(int)response.StatusCode}.");
        var externalId = change.ExternalId;
        if (string.IsNullOrWhiteSpace(externalId) && method == HttpMethod.Post)
        {
            try
            {
                using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                externalId = body.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
            }
            catch (System.Text.Json.JsonException) { }
        }
        return new(true, externalId);
    }

    private async Task<string?> GetTokenAsync(string tokenUrl, string clientId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && _tokenExpiresAt > DateTime.UtcNow.AddMinutes(1))
            return _accessToken;
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) && _tokenExpiresAt > DateTime.UtcNow.AddMinutes(1))
                return _accessToken;
            var secret = await secrets.GetSecretAsync(clientId, ct);
            if (string.IsNullOrWhiteSpace(secret)) return null;
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = secret,
                    ["scope"] = config["Provisioning:Entra:Scope"] ?? "https://graph.microsoft.com/.default"
                })
            };
            using var tokenResponse = await clients.CreateClient("directory-provisioning").SendAsync(tokenRequest, ct);
            if (!tokenResponse.IsSuccessStatusCode) return null;
            using var tokenJson = System.Text.Json.JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(ct));
            _accessToken = tokenJson.RootElement.TryGetProperty("access_token", out var access) ? access.GetString() : null;
            var expires = tokenJson.RootElement.TryGetProperty("expires_in", out var value) ? value.GetInt32() : 300;
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(60, expires));
            return _accessToken;
        }
        finally { _tokenLock.Release(); }
    }
}

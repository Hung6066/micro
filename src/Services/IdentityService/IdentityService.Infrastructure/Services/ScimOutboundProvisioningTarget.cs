using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using His.Hope.IdentityService.Application.Provisioning;
using Microsoft.Extensions.Configuration;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>OAuth client-credentials SCIM outbound adapter.</summary>
public sealed class ScimOutboundProvisioningTarget(
    IHttpClientFactory httpClientFactory,
    VaultClientSecretStore secrets,
    IConfiguration configuration) : IProvisioningTarget
{
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTime _tokenExpiresAt;
    public string Name => "scim";

    public async Task<ProvisioningResult> ApplyAsync(ProvisioningChange change, CancellationToken ct = default)
    {
        var baseUrl = configuration["Provisioning:Scim:BaseUrl"]?.TrimEnd('/');
        var tokenUrl = configuration["Provisioning:Scim:TokenUrl"];
        var clientId = configuration["Provisioning:Scim:ClientId"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(tokenUrl) || string.IsNullOrWhiteSpace(clientId))
            return new(false, Error: "SCIM outbound provisioning is not configured.");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var scimBase) || scimBase.Scheme != Uri.UriSchemeHttps ||
            !Uri.TryCreate(tokenUrl, UriKind.Absolute, out var scimToken) || scimToken.Scheme != Uri.UriSchemeHttps)
            return new(false, Error: "SCIM provisioning endpoints must use HTTPS.");

        var token = await GetTokenAsync(tokenUrl, clientId, ct);
        if (token is null) return new(false, Error: "Unable to obtain SCIM OAuth token.");

        var resource = change.ResourceType.Equals("Group", StringComparison.OrdinalIgnoreCase) ? "Groups" : "Users";
        var endpoint = $"{baseUrl}/{resource}";
        if (!string.IsNullOrWhiteSpace(change.ExternalId)) endpoint += $"/{Uri.EscapeDataString(change.ExternalId)}";
        using var request = new HttpRequestMessage(
            change.Operation.Equals("delete", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Delete :
            change.Operation.Equals("create", StringComparison.OrdinalIgnoreCase) ? HttpMethod.Post : HttpMethod.Patch,
            endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/scim+json"));
        if (request.Method != HttpMethod.Delete)
        {
            request.Content = new StringContent(change.Payload.RootElement.GetRawText(), Encoding.UTF8, "application/scim+json");
        }

        using var response = await httpClientFactory.CreateClient("directory-provisioning").SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return new(false, Error: $"SCIM returned {(int)response.StatusCode}: {body[..Math.Min(body.Length, 500)]}");
        string? externalId = null;
        if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK)
        {
            try { externalId = JsonDocument.Parse(body).RootElement.TryGetProperty("id", out var id) ? id.GetString() : null; }
            catch (JsonException) { }
        }
        return new(true, externalId);
    }

    private async Task<string?> GetTokenAsync(string tokenUrl, string clientId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && _tokenExpiresAt > DateTime.UtcNow.AddMinutes(1)) return _accessToken;
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) && _tokenExpiresAt > DateTime.UtcNow.AddMinutes(1)) return _accessToken;
            var secret = await secrets.GetSecretAsync(clientId, ct);
            if (string.IsNullOrWhiteSpace(secret)) return null;
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = secret,
                    ["scope"] = configuration["Provisioning:Scim:Scope"] ?? "scim.write"
                })
            };
            using var response = await httpClientFactory.CreateClient("directory-provisioning").SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            _accessToken = document.RootElement.GetProperty("access_token").GetString();
            var expires = document.RootElement.TryGetProperty("expires_in", out var value) ? value.GetInt32() : 300;
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(60, expires));
            return _accessToken;
        }
        finally { _tokenLock.Release(); }
    }
}

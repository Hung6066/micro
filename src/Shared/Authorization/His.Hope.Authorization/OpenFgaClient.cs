using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace His.Hope.Authorization;

public interface IOpenFgaClient
{
    Task<bool?> CheckAsync(string subject, string relation, string resource, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>?> ListObjectsAsync(string subject, string relation, string objectType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional OpenFGA adapter used only by the P2 shadow/canary seam. A missing
/// endpoint or failed request returns null; callers must retain local
/// fail-closed authorization and never use this adapter as an implicit grant.
/// </summary>
public sealed class OpenFgaClient(HttpClient httpClient, IConfiguration configuration) : IOpenFgaClient
{
    private readonly string _storeId = configuration["AUTHZ_OPENFGA_STORE_ID"] ?? string.Empty;
    private readonly string _modelId = configuration["AUTHZ_OPENFGA_MODEL_ID"] ?? string.Empty;
    private readonly string _token = configuration["AUTHZ_OPENFGA_TOKEN"] ?? string.Empty;

    public async Task<bool?> CheckAsync(string subject, string relation, string resource, CancellationToken cancellationToken = default)
    {
        if (!Configured()) return null;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"stores/{Uri.EscapeDataString(_storeId)}/check")
        {
            Content = JsonContent.Create(new { tuple_key = new { user = subject, relation, @object = resource }, authorization_model_id = _modelId })
        };
        AddToken(request);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.TryGetProperty("allowed", out var allowed) && allowed.ValueKind is JsonValueKind.True or JsonValueKind.False ? allowed.GetBoolean() : null;
    }

    public async Task<IReadOnlyList<string>?> ListObjectsAsync(string subject, string relation, string objectType, CancellationToken cancellationToken = default)
    {
        if (!Configured()) return null;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"stores/{Uri.EscapeDataString(_storeId)}/list-objects")
        {
            Content = JsonContent.Create(new { type = objectType, user = subject, relation, authorization_model_id = _modelId })
        };
        AddToken(request);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("objects", out var objects) || objects.ValueKind != JsonValueKind.Array) return [];
        return objects.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!).ToArray();
    }

    private bool Configured() => !string.IsNullOrWhiteSpace(_storeId) && !string.IsNullOrWhiteSpace(_modelId) && httpClient.BaseAddress is not null;

    private void AddToken(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }
}

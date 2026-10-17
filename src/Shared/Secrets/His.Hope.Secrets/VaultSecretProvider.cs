using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace His.Hope.Secrets;

public interface IVaultSecretProvider
{
    Task<string?> GetAsync(string path, string key, CancellationToken cancellationToken = default);
    Task PutAsync(string path, string key, string value, CancellationToken cancellationToken = default);
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Shared KV-v2 adapter. Services declare a logical path/key and never own
/// Vault HTTP, token or response parsing details.
/// </summary>
public sealed class VaultSecretProvider(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<VaultOptions> options,
    IVaultTokenProvider tokenProvider) : IVaultSecretProvider
{
    public async Task<string?> GetAsync(string path, string key, CancellationToken cancellationToken = default)
    {
        Validate(path, key);
        using var response = await SendAsync(HttpMethod.Get, DataPath(path), null, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.TryGetProperty("data", out var data) &&
               data.TryGetProperty("data", out var values) &&
               values.TryGetProperty(key, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    public async Task PutAsync(string path, string key, string value, CancellationToken cancellationToken = default)
    {
        Validate(path, key);
        var payload = JsonSerializer.Serialize(new { data = new Dictionary<string, string> { [key] = value } });
        using var response = await SendAsync(
            HttpMethod.Post,
            DataPath(path),
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        ValidatePath(path);
        using var response = await SendAsync(HttpMethod.Delete, DataPath(path), null, cancellationToken);
        if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var current = options.CurrentValue;
        if (!Uri.TryCreate(current.Address, UriKind.Absolute, out var address))
            throw new InvalidOperationException("Vault address is not configured.");

        var client = httpClientFactory.CreateClient("vault");
        client.BaseAddress = address;
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add("X-Vault-Token", await tokenProvider.GetTokenAsync(cancellationToken));
        return await client.SendAsync(request, cancellationToken);
    }

    private string DataPath(string path)
    {
        var segments = $"{options.CurrentValue.SecretsPathPrefix.Trim('/')}/{path.Trim('/')}"
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Select(Uri.EscapeDataString);
        return $"/v1/{Uri.EscapeDataString(options.CurrentValue.SecretsMount.Trim('/'))}/data/{string.Join('/', segments)}";
    }

    private static void Validate(string path, string key)
    {
        ValidatePath(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
    }

    private static void ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or ".."))
            throw new ArgumentException("Vault secret paths cannot contain traversal segments.", nameof(path));
    }
}

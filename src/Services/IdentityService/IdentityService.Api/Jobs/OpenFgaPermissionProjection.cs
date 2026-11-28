using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace His.Hope.IdentityService.Api.Jobs;

public sealed record OpenFgaPermissionTuple(string User, string Relation, string Object);
public sealed record OpenFgaPermissionProjectionResult(int Removed, int Added);

public sealed class OpenFgaPermissionProjection
{
    public const string HttpClientName = "identity-openfga-permission-projection";
    public const string StoreName = "micro-identity-canonical-permissions";
    public const string Relation = "granted";
    private readonly HttpClient _http;
    private readonly string _storePath;
    private readonly string _modelId;

    public OpenFgaPermissionProjection(HttpClient http, IConfiguration configuration)
    {
        if (configuration["AUTHZ_OPENFGA_PERMISSION_RELATION"] != Relation)
            throw new InvalidOperationException("OpenFGA permission projection requires the granted permission relation.");
        var endpoint = Required(configuration, "AUTHZ_OPENFGA_URL");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("OpenFGA permission projection requires an absolute HTTP(S) endpoint.");
        http.BaseAddress = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromSeconds(10);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", Required(configuration, "AUTHZ_OPENFGA_TOKEN"));
        _http = http;
        _storePath = $"stores/{Uri.EscapeDataString(Required(configuration, "AUTHZ_OPENFGA_STORE_ID"))}";
        _modelId = Required(configuration, "AUTHZ_OPENFGA_MODEL_ID");
    }

    public async Task<OpenFgaPermissionProjectionResult> ReconcileAsync(
        IReadOnlySet<OpenFgaPermissionTuple> desired,
        CancellationToken ct = default)
    {
        if (desired.Any(tuple => !IsOwnedTuple(tuple)))
            throw new InvalidOperationException("Desired OpenFGA tuples must be direct user permission grants.");
        using (var response = await _http.GetAsync(_storePath, ct))
        {
            response.EnsureSuccessStatusCode();
            using var store = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (store.RootElement.GetProperty("name").GetString() != StoreName)
                throw new InvalidOperationException("OpenFGA permission projection refuses an unowned store.");
        }

        var existing = await ReadTuplesAsync(ct);
        var removed = existing.Except(desired).ToArray();
        var added = desired.Except(existing).ToArray();
        await WriteBatchesAsync("deletes", removed, ct);
        await WriteBatchesAsync("writes", added, ct);
        return new OpenFgaPermissionProjectionResult(removed.Length, added.Length);
    }

    private async Task<HashSet<OpenFgaPermissionTuple>> ReadTuplesAsync(CancellationToken ct)
    {
        var result = new HashSet<OpenFgaPermissionTuple>();
        var continuation = "";
        var seenTokens = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            using var response = await _http.PostAsJsonAsync(
                $"{_storePath}/read", new { page_size = 100, continuation_token = continuation }, ct);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            foreach (var item in document.RootElement.GetProperty("tuples").EnumerateArray())
            {
                var key = item.GetProperty("key");
                var tuple = new OpenFgaPermissionTuple(
                    key.GetProperty("user").GetString() ?? throw new JsonException("OpenFGA tuple user is missing."),
                    key.GetProperty("relation").GetString() ?? throw new JsonException("OpenFGA tuple relation is missing."),
                    key.GetProperty("object").GetString() ?? throw new JsonException("OpenFGA tuple object is missing."));
                if (!IsOwnedTuple(tuple))
                    throw new InvalidOperationException("OpenFGA permission store contains unmanaged tuples.");
                result.Add(tuple);
            }
            continuation = document.RootElement.TryGetProperty("continuation_token", out var token)
                ? token.GetString() ?? "" : "";
            if (continuation.Length > 0 && !seenTokens.Add(continuation))
                throw new InvalidOperationException("OpenFGA tuple pagination repeated a continuation token.");
        } while (continuation.Length > 0);
        return result;
    }

    private async Task WriteBatchesAsync(
        string operation, OpenFgaPermissionTuple[] tuples, CancellationToken ct)
    {
        foreach (var batch in tuples.Chunk(100))
        {
            var payload = new Dictionary<string, object>
            {
                ["authorization_model_id"] = _modelId,
                [operation] = new { tuple_keys = batch }
            };
            using var response = await _http.PostAsJsonAsync($"{_storePath}/write", payload, ct);
            response.EnsureSuccessStatusCode();
        }
    }

    private static bool IsOwnedTuple(OpenFgaPermissionTuple tuple) =>
        tuple.Relation == Relation &&
        tuple.User.StartsWith("user:", StringComparison.Ordinal) &&
        Guid.TryParse(tuple.User["user:".Length..], out _) &&
        tuple.Object.StartsWith("permission:", StringComparison.Ordinal) &&
        tuple.Object.Length > "permission:".Length;

    private static string Required(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key]) ? configuration[key]!
            : throw new InvalidOperationException($"OpenFGA permission projection requires {key}.");
}

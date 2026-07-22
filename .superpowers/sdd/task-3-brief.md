### Task 3: Prometheus Instant Query (QueryAsync)

**Files:**
- Modify: `src/Bff/SystemDashboard.Bff/Services/IPrometheusQueryService.cs:13-14`
- Modify: `src/Bff/SystemDashboard.Bff/Services/PrometheusQueryService.cs` (add method + response types)

**Interfaces:**
- Consumes: (none)
- Produces: `Task<MetricDataPoint?> QueryAsync(string query, CancellationToken ct = default)`

- [ ] **Step 1: Add QueryAsync to IPrometheusQueryService**

```csharp
// Add after line 14 in IPrometheusQueryService.cs:
    Task<MetricDataPoint?> QueryAsync(string query, CancellationToken ct = default);
```

- [ ] **Step 2: Implement QueryAsync in PrometheusQueryService**

Add the method to `PrometheusQueryService` class (before the `#region` or after `QueryRangeAsync`):

```csharp
public async Task<MetricDataPoint?> QueryAsync(
    string query, CancellationToken ct = default)
{
    try
    {
        var requestUri = $"/api/v1/query?query={Uri.EscapeDataString(query)}";

        var response = await _httpClient.GetAsync(requestUri, ct);
        response.EnsureSuccessStatusCode();

        var promResponse = await response.Content.ReadFromJsonAsync<PromInstantResponse>(ct);
        var result = promResponse?.Data?.Result?.FirstOrDefault();
        if (result?.Value is null)
            return null;

        var valElement = (JsonElement)result.Value[1];
        return new MetricDataPoint
        {
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(
                ((JsonElement)result.Value[0]).GetInt64()).UtcDateTime,
            Value = double.TryParse(valElement.GetString(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0.0
        };
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to query Prometheus instant: {Query}", query);
        return null;
    }
}
```

Also add these private nested records inside `PrometheusQueryService` (after `PromResult` on line 93):

```csharp
private sealed record PromInstantResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("data")]
    public PromInstantData? Data { get; init; }
}

private sealed record PromInstantData
{
    [JsonPropertyName("resultType")]
    public string? ResultType { get; init; }

    [JsonPropertyName("result")]
    public PromInstantResult[]? Result { get; init; }
}

private sealed record PromInstantResult
{
    [JsonPropertyName("metric")]
    public Dictionary<string, string>? Metric { get; init; }

    [JsonPropertyName("value")]
    public List<object>? Value { get; init; }
}
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj --no-restore`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Bff/SystemDashboard.Bff/Services/IPrometheusQueryService.cs src/Bff/SystemDashboard.Bff/Services/PrometheusQueryService.cs
git commit -m "feat(dashboard): add Prometheus instant query (QueryAsync) for single-value lookups"
```

---


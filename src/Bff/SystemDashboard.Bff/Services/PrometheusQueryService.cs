using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Services;

public sealed class PrometheusQueryService : IPrometheusQueryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PrometheusQueryService> _logger;

    public PrometheusQueryService(
        HttpClient httpClient,
        ILogger<PrometheusQueryService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<MetricDataPoint>> QueryRangeAsync(
        string query, DateTime start, DateTime end, string step, CancellationToken ct = default)
    {
        try
        {
            var startUnix = new DateTimeOffset(start).ToUnixTimeSeconds();
            var endUnix = new DateTimeOffset(end).ToUnixTimeSeconds();

            var requestUri = $"/api/v1/query_range?query={Uri.EscapeDataString(query)}" +
                             $"&start={startUnix}&end={endUnix}&step={Uri.EscapeDataString(step)}";

            var response = await _httpClient.GetAsync(requestUri, ct);
            response.EnsureSuccessStatusCode();

            var promResponse = await response.Content.ReadFromJsonAsync<PromQueryResponse>(ct);
            if (promResponse?.Data?.Result is null)
                return [];

            // Take the first result series; if multiple series exist, merge their values
            var dataPoints = promResponse.Data.Result
                .SelectMany(r => r.Values ?? [])
                .Select(v =>
                {
                    var tsElement = (JsonElement)v[0];
                    var valElement = (JsonElement)v[1];
                    return new MetricDataPoint
                    {
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(tsElement.GetInt64()).UtcDateTime,
                        Value = double.TryParse(valElement.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                            ? parsed
                            : 0.0
                    };
                })
                .OrderBy(p => p.Timestamp)
                .ToList();

            return dataPoints;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Prometheus: {Query}", query);
            return [];
        }
    }

    public async Task<List<PrometheusSample>> QuerySamplesAsync(
        string query, CancellationToken ct = default)
    {
        var requestUri = $"/api/v1/query?query={Uri.EscapeDataString(query)}";
        _logger.LogInformation("QuerySamples URI: {Uri}", requestUri);

        try
        {
            var response = await _httpClient.GetAsync(requestUri, ct);
            _logger.LogInformation("QuerySamples response status: {Status}", response.StatusCode);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("QuerySamples body (first 200): {Body}", body[..Math.Min(body.Length, 200)]);

            var promResponse = JsonSerializer.Deserialize<PromInstantResponse>(body, _jsonOptions);
            if (promResponse?.Data?.Result is null || promResponse.Data.Result.Length == 0)
            {
                _logger.LogInformation("QuerySamples no results for: {Query}", query);
                return [];
            }

            var samples = new List<PrometheusSample>(promResponse.Data.Result.Length);
            foreach (var result in promResponse.Data.Result)
            {
                if (result.Value is null || result.Value.Count < 2)
                    continue;

                var tsElement = (JsonElement)result.Value[0];
                var valElement = (JsonElement)result.Value[1];

                if (!double.TryParse(valElement.GetString(), NumberStyles.Any,
                        CultureInfo.InvariantCulture, out var parsed))
                    continue;

                _logger.LogInformation("QuerySamples parsed: metric={Metric}, val={Val}",
                    string.Join(",", (result.Metric ?? []).Select(kv => $"{kv.Key}={kv.Value}")), parsed);

                samples.Add(new PrometheusSample
                {
                    Labels = result.Metric ?? [],
                    Value = parsed,
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds(tsElement.GetInt64()).UtcDateTime,
                });
            }

            return samples;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Prometheus samples: {Query}", query);
            return [];
        }
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

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

    // Private nested records for Prometheus JSON response deserialization
    private sealed record PromQueryResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("data")]
        public PromData? Data { get; init; }
    }

    private sealed record PromData
    {
        [JsonPropertyName("resultType")]
        public string? ResultType { get; init; }

        [JsonPropertyName("result")]
        public PromResult[]? Result { get; init; }
    }

    private sealed record PromResult
    {
        [JsonPropertyName("metric")]
        public Dictionary<string, string>? Metric { get; init; }

        [JsonPropertyName("values")]
        public List<List<object>>? Values { get; init; }
    }

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
}

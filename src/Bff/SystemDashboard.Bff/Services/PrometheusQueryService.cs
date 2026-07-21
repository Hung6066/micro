using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Services;

public sealed class PrometheusQueryService : IPrometheusQueryService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<PrometheusOptions> _options;
    private readonly ILogger<PrometheusQueryService> _logger;

    public PrometheusQueryService(
        HttpClient httpClient,
        IOptions<PrometheusOptions> options,
        ILogger<PrometheusQueryService> logger)
    {
        _httpClient = httpClient;
        _options = options;
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
                .Select(v => new MetricDataPoint
                {
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)v[0]).UtcDateTime,
                    Value = Convert.ToDouble(v[1], CultureInfo.InvariantCulture)
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
}

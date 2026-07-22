using System.Net.Http.Json;
using System.Text.Json.Serialization;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Services;

public sealed class ElasticsearchQueryService : IElasticsearchQueryService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<ElasticsearchOptions> _options;
    private readonly ILogger<ElasticsearchQueryService> _logger;

    public ElasticsearchQueryService(
        HttpClient httpClient,
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchQueryService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<List<LogEntry>> QueryLogsAsync(
        string? service = null, string? level = null,
        int? from = null, int size = 100,
        string? searchQuery = null,
        DateTime? afterTimestamp = null,
        CancellationToken ct = default)
    {
        try
        {
            var mustClauses = new List<object>();

            if (!string.IsNullOrWhiteSpace(service))
                mustClauses.Add(new { term = new Dictionary<string, object> { ["service.keyword"] = service } });

            if (!string.IsNullOrWhiteSpace(level))
                mustClauses.Add(new { match = new { level } });

            if (!string.IsNullOrWhiteSpace(searchQuery))
                mustClauses.Add(new { query_string = new { query = searchQuery } });

            if (afterTimestamp.HasValue)
            {
                mustClauses.Add(new
                {
                    range = new Dictionary<string, object>
                    {
                        ["@timestamp"] = new Dictionary<string, object>
                        {
                            ["gte"] = afterTimestamp.Value.ToString("o")
                        }
                    }
                });
            }

            var requestBody = new
            {
                query = (object)new Dictionary<string, object>
                {
                    ["bool"] = new Dictionary<string, object?>
                    {
                        ["must"] = mustClauses.Count > 0 ? mustClauses : null,
                    }
                },
                sort = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["@timestamp"] = new { order = "desc" }
                    }
                },
                from = from ?? 0,
                size
            };

            var logIndex = _options.Value.LogIndex;
            var response = await _httpClient.PostAsJsonAsync(
                $"/{logIndex}/_search", requestBody, ct);

            response.EnsureSuccessStatusCode();

            var esResponse = await response.Content.ReadFromJsonAsync<EsResponse>(ct);
            if (esResponse?.Hits?.HitsList is null)
                return [];

            return esResponse.Hits.HitsList
                .Where(h => h.Source is not null)
                .Select(h => new LogEntry
                {
                    Id = h.Id,
                    Timestamp = h.Source!.Timestamp,
                    Level = h.Source.Level ?? "",
                    Service = h.Source.Service ?? "",
                    Message = h.Source.Message ?? "",
                    TraceId = h.Source.TraceId,
                    SpanId = h.Source.SpanId,
                    Exception = h.Source.Exception,
                    Properties = h.Source.Fields
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Elasticsearch logs");
            return [];
        }
    }

    // Private nested records for ES response deserialization
    private sealed record EsResponse
    {
        [JsonPropertyName("hits")]
        public EsHits? Hits { get; init; }
    }

    private sealed record EsHits
    {
        [JsonPropertyName("hits")]
        public EsHit[]? HitsList { get; init; }
    }

    private sealed record EsHit
    {
        [JsonPropertyName("_id")]
        public string? Id { get; init; }

        [JsonPropertyName("_source")]
        public EsLogSource? Source { get; init; }
    }

    private sealed record EsLogSource
    {
        [JsonPropertyName("@timestamp")]
        public DateTime Timestamp { get; init; }

        public string? Level { get; init; }
        public string? Service { get; init; }
        public string? Message { get; init; }

        [JsonPropertyName("traceId")]
        public string? TraceId { get; init; }

        [JsonPropertyName("spanId")]
        public string? SpanId { get; init; }

        public string? Exception { get; init; }

        public Dictionary<string, object>? Fields { get; init; }
    }
}

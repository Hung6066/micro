using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Services;

public sealed class LokiOptions
{
    public const string SectionName = "Loki";
    public required string Url { get; init; }
    public required string DefaultQuery { get; init; }
}

public sealed class LokiQueryService : ILogQueryService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<LokiOptions> _options;
    private readonly ILogger<LokiQueryService> _logger;

    public LokiQueryService(HttpClient httpClient, IOptions<LokiOptions> options, ILogger<LokiQueryService> logger)
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
            var query = _options.Value.DefaultQuery;
            if (!string.IsNullOrWhiteSpace(service))
                query = AddServiceSelector(query, service);
            if (!string.IsNullOrWhiteSpace(level))
                query += $" |= `level={EscapeLogQl(level)}`";
            if (!string.IsNullOrWhiteSpace(searchQuery))
                query += $" |= `{EscapeLogQl(searchQuery)}`";

            var end = DateTimeOffset.UtcNow;
            var start = afterTimestamp.HasValue
                ? new DateTimeOffset(afterTimestamp.Value.ToUniversalTime())
                : end.AddHours(-1);
            var limit = Math.Clamp(size, 1, 5000);
            var uri = $"/loki/api/v1/query_range?query={Uri.EscapeDataString(query)}" +
                      $"&limit={limit}&direction=backward&start={ToUnixTimeNanoseconds(start)}&end={ToUnixTimeNanoseconds(end)}";

            using var response = await _httpClient.GetAsync(uri, ct);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<LokiResponse>(cancellationToken: ct);
            if (payload?.Data?.Result is null)
                return [];

            return payload.Data.Result
                .SelectMany(stream => stream.Values.Select(value => Map(value, stream.Stream)))
                .Where(log => string.IsNullOrWhiteSpace(service) ||
                              string.Equals(log.Service, service, StringComparison.OrdinalIgnoreCase))
                .Where(log => string.IsNullOrWhiteSpace(level) ||
                              string.Equals(log.Level, level, StringComparison.OrdinalIgnoreCase))
                .Where(log => string.IsNullOrWhiteSpace(searchQuery) ||
                              log.Message.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(log => log.Timestamp)
                .Skip(Math.Max(from ?? 0, 0))
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Loki logs");
            return [];
        }
    }

    private static LogEntry Map(IReadOnlyList<string> value, Dictionary<string, string> labels)
    {
        var timestamp = FromUnixTimeNanoseconds(long.Parse(value[0])).UtcDateTime;
        var message = value.Count > 1 ? value[1] : string.Empty;
        var filenameService = ExtractContainer(labels.GetValueOrDefault("filename"));
        var labeledService = labels.GetValueOrDefault("service_name");
        var service = filenameService ??
                      labels.GetValueOrDefault("container") ??
                      (string.Equals(labeledService, "kubernetes-pods", StringComparison.OrdinalIgnoreCase)
                          ? null
                          : labeledService) ?? "unknown";
        var level = labels.GetValueOrDefault("level") ?? DetectLevel(message);

        return new LogEntry
        {
            Id = $"{timestamp.Ticks}:{service}",
            Timestamp = timestamp,
            Level = level,
            Service = service,
            Message = message,
            TraceId = ExtractToken(message, "traceId"),
            SpanId = ExtractToken(message, "spanId"),
            Properties = labels.ToDictionary(pair => pair.Key, pair => (object)pair.Value)
        };
    }

    private static string EscapeLogQl(string value) => value.Replace("\\", "\\\\").Replace("`", "\\`");

    private static string AddServiceSelector(string query, string service)
    {
        // Promtail stores the workload name in the pod filename, while the
        // service_name label is currently the generic "kubernetes-pods".
        // Restricting Loki at query time prevents unrelated streams from
        // consuming the requested limit before the defensive client filter.
        var escapedService = Regex.Escape(service);
        var selector = $"filename=~\"/var/log/pods/his-hope-dev_{escapedService}-.*\"";
        var selectorEnd = query.IndexOf('}');
        if (query.StartsWith('{') && selectorEnd >= 0)
        {
            var separator = query[..selectorEnd].TrimEnd().EndsWith('{') ? string.Empty : ",";
            return query.Insert(selectorEnd, $"{separator}{selector}");
        }

        return $"{{{selector}}} |= {EscapeLogQl(query)}";
    }

    private static long ToUnixTimeNanoseconds(DateTimeOffset value) =>
        (value.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100;

    private static DateTimeOffset FromUnixTimeNanoseconds(long value) =>
        DateTimeOffset.UnixEpoch.AddTicks(value / 100);

    private static string? ExtractContainer(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return null;
        var parts = filename.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[^2] : null;
    }

    private static string DetectLevel(string message) =>
        message.Contains("error", StringComparison.OrdinalIgnoreCase) ? "error" : "info";

    private static string? ExtractToken(string message, string name)
    {
        var marker = name + "=";
        var start = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += marker.Length;
        var end = message.IndexOfAny([' ', ',', ';', '"'], start);
        return message[start..(end < 0 ? message.Length : end)];
    }

    private sealed record LokiResponse
    {
        [JsonPropertyName("data")] public LokiData? Data { get; init; }
    }

    private sealed record LokiData
    {
        [JsonPropertyName("result")] public List<LokiStream>? Result { get; init; }
    }

    private sealed record LokiStream
    {
        [JsonPropertyName("stream")] public Dictionary<string, string> Stream { get; init; } = [];
        [JsonPropertyName("values")] public List<List<string>> Values { get; init; } = [];
    }
}

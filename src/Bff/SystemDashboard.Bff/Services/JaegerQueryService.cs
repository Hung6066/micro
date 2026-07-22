using System.Net.Http.Json;
using System.Text.Json.Serialization;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Services;

public sealed class JaegerQueryService : IJaegerQueryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JaegerQueryService> _logger;

    public JaegerQueryService(
        HttpClient httpClient,
        ILogger<JaegerQueryService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<TraceSummary>> SearchTracesAsync(
        string service, DateTime? from, DateTime? to,
        long? minDurationMs, int limit = 20, CancellationToken ct = default)
    {
        try
        {
            var query = $"limit={limit}";
            if (!string.IsNullOrEmpty(service))
                query += $"&service={Uri.EscapeDataString(service)}";

            if (from.HasValue)
                query += $"&start={ToUnixTimeMicroseconds(new DateTimeOffset(from.Value))}";
            if (to.HasValue)
                query += $"&end={ToUnixTimeMicroseconds(new DateTimeOffset(to.Value))}";
            if (minDurationMs.HasValue)
                query += $"&minDuration={minDurationMs.Value}ms";

            var uri = $"/api/traces?{query}";
            var response = await _httpClient.GetAsync(uri, ct);
            response.EnsureSuccessStatusCode();

            var jaegerResponse = await response.Content.ReadFromJsonAsync<JaegerSearchResponse>(ct);
            if (jaegerResponse?.Data is null)
                return [];

            return jaegerResponse.Data
                .Select(t => MapToTraceSummary(t))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to search Jaeger traces for service {Service}", service);
            return [];
        }
    }

    public async Task<TraceDetail?> GetTraceAsync(string traceId, CancellationToken ct = default)
    {
        try
        {
            var uri = $"/api/traces/{Uri.EscapeDataString(traceId)}";

            var response = await _httpClient.GetAsync(uri, ct);
            response.EnsureSuccessStatusCode();

            var jaegerResponse = await response.Content.ReadFromJsonAsync<JaegerSearchResponse>(ct);
            var trace = jaegerResponse?.Data?.FirstOrDefault();
            if (trace is null)
                return null;

            return MapToTraceDetail(trace);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Jaeger trace {TraceId}", traceId);
            return null;
        }
    }

    private static TraceSummary MapToTraceSummary(JaegerTrace trace)
    {
        var rootSpan = trace.Spans?.FirstOrDefault(s => s.References is null || s.References.Count == 0);
        var startTimeUs = trace.StartTimeUs;

        // Resolve process ID to actual service name
        var rootService = "";
        if (rootSpan?.ProcessId is not null && trace.Processes?.TryGetValue(rootSpan.ProcessId, out var proc) == true)
            rootService = proc.ServiceName ?? "";

        var durationUs = (trace.Spans?.Max(s => s.StartTime + s.Duration) ?? startTimeUs) - startTimeUs;

        return new TraceSummary
        {
            TraceId = trace.TraceId ?? "",
            RootService = rootService,
            RootName = rootSpan?.OperationName ?? "",
            DurationMs = durationUs / 1000,
            SpanCount = trace.Spans?.Count ?? 0,
            StartTime = FromUnixTimeMicroseconds(startTimeUs).UtcDateTime,
            Status = "Ok",
            HasErrors = trace.Spans?.Any(s => s.Tags?.Any(t => t.Key == "error" && t.Value?.ToString() == "true") == true) ?? false
        };
    }

    private static TraceDetail MapToTraceDetail(JaegerTrace trace)
    {
        var processes = trace.Processes?
            .Where(p => p.Key is not null)
            .ToDictionary(p => p.Key, p => p.Value?.ServiceName ?? p.Key) ?? [];

        var startTimeUs = trace.StartTimeUs;
        var endTimeUs = trace.Spans?.Max(s => s.StartTime + s.Duration) ?? startTimeUs;
        var durationUs = endTimeUs - startTimeUs;

        var spans = trace.Spans?.Select(s =>
        {
            var spanStart = FromUnixTimeMicroseconds(s.StartTime);
            var spanEnd = FromUnixTimeMicroseconds(s.StartTime + s.Duration);

            // Resolve parent span ID from references (first CHILD_OF)
            var parentSpanId = s.References?
                .FirstOrDefault(r => r.RefType == "CHILD_OF" || r.RefType == "FOLLOWS_FROM")
                ?.SpanId;

            // Resolve service name from process ID
            var service = s.ProcessId is not null && processes.TryGetValue(s.ProcessId, out var svc)
                ? svc
                : s.ProcessId ?? "";

            return new TraceSpanEx
            {
                SpanId = s.SpanId ?? "",
                ParentSpanId = parentSpanId,
                Name = s.OperationName ?? "",
                Service = service,
                StartTime = spanStart.UtcDateTime,
                EndTime = spanEnd.UtcDateTime,
                DurationMs = s.Duration / 1000.0,
                Status = "Ok",
                Attributes = s.Tags?
                    .Where(t => t.Key is not null && t.Value is not null)
                    .ToDictionary(t => t.Key!, t => t.Value?.ToString() ?? "") ?? null,
                Events = s.Logs?.Select(l => new TraceSpanEvent
                {
                    Name = l.Fields?.FirstOrDefault(f => f.Key == "event")?.Value?.ToString()
                           ?? l.Fields?.FirstOrDefault()?.Value?.ToString()
                           ?? "log",
                    Timestamp = FromUnixTimeMicroseconds(l.Timestamp).UtcDateTime,
                    Attributes = l.Fields?.Where(f => f.Key is not null)
                        .ToDictionary(f => f.Key!, f => f.Value?.ToString() ?? "")
                }).ToList() ?? null
            };
        }).ToList() ?? [];

        var serviceNames = processes.Values.Distinct().OrderBy(x => x).ToList();

        return new TraceDetail
        {
            TraceId = trace.TraceId ?? "",
            RootService = spans.FirstOrDefault()?.Service ?? serviceNames.FirstOrDefault() ?? "",
            RootName = spans.FirstOrDefault()?.Name ?? "",
            StartTime = FromUnixTimeMicroseconds(startTimeUs).UtcDateTime,
            EndTime = FromUnixTimeMicroseconds(endTimeUs).UtcDateTime,
            DurationMs = durationUs / 1000,
            SpanCount = spans.Count,
            Status = "Ok",
            Spans = spans,
            Services = serviceNames
        };
    }

    private static long ToUnixTimeMicroseconds(DateTimeOffset dto) =>
        (dto.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) / 10;

    private static DateTimeOffset FromUnixTimeMicroseconds(long microseconds) =>
        DateTimeOffset.UnixEpoch.AddTicks(microseconds * 10);

    // Private nested records for Jaeger JSON response deserialization
    private sealed record JaegerSearchResponse
    {
        [JsonPropertyName("data")]
        public List<JaegerTrace>? Data { get; init; }

        [JsonPropertyName("total")]
        public int Total { get; init; }

        [JsonPropertyName("limit")]
        public int Limit { get; init; }

        [JsonPropertyName("offset")]
        public int Offset { get; init; }

        [JsonPropertyName("errors")]
        public List<JaegerError>? Errors { get; init; }
    }

    private sealed record JaegerError
    {
        [JsonPropertyName("code")]
        public int Code { get; init; }

        [JsonPropertyName("msg")]
        public string? Message { get; init; }

        [JsonPropertyName("traceID")]
        public string? TraceId { get; init; }
    }

    private sealed record JaegerTrace
    {
        [JsonPropertyName("traceID")]
        public string? TraceId { get; init; }

        [JsonPropertyName("spans")]
        public List<JaegerSpan>? Spans { get; init; }

        [JsonPropertyName("processes")]
        public Dictionary<string, JaegerProcess>? Processes { get; init; }

        [JsonPropertyName("warnings")]
        public List<string>? Warnings { get; init; }

        /// <summary>
        /// Start time in microseconds (may be on root trace or computed).
        /// </summary>
        [JsonIgnore]
        public long StartTimeUs => Spans?.Min(s => s.StartTime) ?? 0;
    }

    private sealed record JaegerSpan
    {
        [JsonPropertyName("spanID")]
        public string? SpanId { get; init; }

        [JsonPropertyName("operationName")]
        public string? OperationName { get; init; }

        [JsonPropertyName("startTime")]
        public long StartTime { get; init; }

        [JsonPropertyName("duration")]
        public long Duration { get; init; }

        [JsonPropertyName("processID")]
        public string? ProcessId { get; init; }

        [JsonPropertyName("references")]
        public List<JaegerReference>? References { get; init; }

        [JsonPropertyName("tags")]
        public List<JaegerKeyValue>? Tags { get; init; }

        [JsonPropertyName("logs")]
        public List<JaegerLog>? Logs { get; init; }
    }

    private sealed record JaegerReference
    {
        [JsonPropertyName("traceID")]
        public string? TraceId { get; init; }

        [JsonPropertyName("spanID")]
        public string? SpanId { get; init; }

        [JsonPropertyName("refType")]
        public string? RefType { get; init; }
    }

    private sealed record JaegerKeyValue
    {
        [JsonPropertyName("key")]
        public string? Key { get; init; }

        [JsonPropertyName("value")]
        public object? Value { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }
    }

    private sealed record JaegerLog
    {
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; init; }

        [JsonPropertyName("fields")]
        public List<JaegerKeyValue>? Fields { get; init; }
    }

    private sealed record JaegerProcess
    {
        [JsonPropertyName("serviceName")]
        public string? ServiceName { get; init; }

        [JsonPropertyName("tags")]
        public List<JaegerKeyValue>? Tags { get; init; }
    }
}

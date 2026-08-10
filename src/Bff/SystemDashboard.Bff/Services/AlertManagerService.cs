using System.Net.Http.Json;
using System.Text.Json.Serialization;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Services;

public sealed class AlertManagerOptions
{
    public const string SectionName = "AlertManager";
    public required string Url { get; init; }
}

public interface IAlertManagerService
{
    Task<List<AlertRecord>> GetAlertsAsync(CancellationToken ct = default);
}

public sealed class AlertManagerService : IAlertManagerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AlertManagerService> _logger;

    public AlertManagerService(
        HttpClient httpClient,
        ILogger<AlertManagerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<AlertRecord>> GetAlertsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v2/alerts", ct);
            response.EnsureSuccessStatusCode();

            var amAlerts = await response.Content.ReadFromJsonAsync<List<AlertManagerAlert>>(ct);
            if (amAlerts is null) return [];

            return amAlerts.Select(a => new AlertRecord
            {
                Name = a.Labels?.GetValueOrDefault("alertname") ?? "unknown",
                Status = a.Status?.State ?? "firing",
                Severity = NormalizeSeverity(a.Labels?.GetValueOrDefault("severity")),
                Summary = a.Annotations?.GetValueOrDefault("summary")
                          ?? a.Annotations?.GetValueOrDefault("description")
                          ?? "No summary",
                Service = a.Labels?.GetValueOrDefault("service")
                          ?? a.Labels?.GetValueOrDefault("job")
                          ?? "unknown",
                Instance = a.Labels?.GetValueOrDefault("instance") ?? "",
                StartsAt = a.StartsAt,
                EndsAt = a.EndsAt,
                GeneratorUrl = a.GeneratorUrl ?? "",
                IsSilenced = a.Status?.SilencedBy is { Count: > 0 },
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query AlertManager API");
            return [];
        }
    }

    private static string NormalizeSeverity(string? raw) => raw?.ToLowerInvariant() switch
    {
        "critical" => "critical",
        "warning" or "warn" => "warning",
        "info" => "info",
        _ => "info",
    };

    // ── AlertManager API v2 JSON shapes ──

    private sealed record AlertManagerAlert
    {
        [JsonPropertyName("status")]
        public AlertManagerStatus? Status { get; init; }

        [JsonPropertyName("labels")]
        public Dictionary<string, string>? Labels { get; init; }

        [JsonPropertyName("annotations")]
        public Dictionary<string, string>? Annotations { get; init; }

        [JsonPropertyName("startsAt")]
        public DateTime StartsAt { get; init; }

        [JsonPropertyName("endsAt")]
        public DateTime? EndsAt { get; init; }

        [JsonPropertyName("generatorURL")]
        public string? GeneratorUrl { get; init; }
    }

    private sealed record AlertManagerStatus
    {
        [JsonPropertyName("state")]
        public string? State { get; init; }

        [JsonPropertyName("silencedBy")]
        public List<string>? SilencedBy { get; init; }

        [JsonPropertyName("inhibitedBy")]
        public List<string>? InhibitedBy { get; init; }
    }
}

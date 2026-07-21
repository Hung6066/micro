using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Services;

public sealed class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";
    public required string Url { get; init; }
    public required string LogIndex { get; init; }
}

public interface IElasticsearchQueryService
{
    Task<List<LogEntry>> QueryLogsAsync(
        string? service = null, string? level = null,
        DateTime? from = null, int size = 100,
        string? searchQuery = null, CancellationToken ct = default);
}

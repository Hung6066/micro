using System.Text.Json.Serialization;
using His.Hope.Contracts.Query;

namespace His.Hope.Contracts.Bulk;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BulkJobStatus { Queued, Running, Completed, Failed, Cancelled }

public sealed record BulkJobContract(
    string JobId,
    string Resource,
    string ActionId,
    BulkJobStatus Status,
    int Processed,
    int Total,
    string? ErrorCode = null,
    string? CorrelationId = null,
    IReadOnlyList<BulkJobRowContract>? RowProgress = null,
    string? DownloadUrl = null);

public sealed record BulkJobRowContract(string RowKey, string Status, string? ErrorCode = null);

public sealed record BulkJobRequest(string Resource, string ActionId, IReadOnlyList<string> RowKeys, QueryRequest Query);

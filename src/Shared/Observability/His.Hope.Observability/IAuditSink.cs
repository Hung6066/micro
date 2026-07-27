namespace His.Hope.Observability;

public interface IAuditSink
{
    ValueTask WriteAsync(AuditRecord auditRecord, CancellationToken cancellationToken = default);
}

/// <summary>Marker for an audit sink that has durable persistence semantics.</summary>
public interface IDurableAuditSink : IAuditSink
{
}

public sealed record AuditRecord
{
    public required string Action { get; init; }
    public required string Resource { get; init; }
    public string? SubjectId { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, object?> Properties { get; init; } =
        new Dictionary<string, object?>();
}

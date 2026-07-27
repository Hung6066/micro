using His.Hope.Infrastructure.Audit;
using His.Hope.Observability;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>Bridges the shared observability audit contract to Identity's durable database audit pipeline.</summary>
public sealed class IdentityObservabilityAuditSink : IDurableAuditSink
{
    private readonly IAuditService _auditService;

    public IdentityObservabilityAuditSink(IAuditService auditService) => _auditService = auditService;

    public ValueTask WriteAsync(AuditRecord auditRecord, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resourceId = auditRecord.Properties.TryGetValue("resourceId", out var value)
            ? value?.ToString()
            : null;

        _auditService.LogPhiAccess(new PhiAuditEntry
        {
            UserId = auditRecord.SubjectId ?? "system",
            ResourceType = auditRecord.Resource,
            ResourceId = resourceId ?? auditRecord.Resource,
            Action = auditRecord.Action,
            Timestamp = auditRecord.OccurredAt.UtcDateTime,
            CorrelationId = auditRecord.Properties.TryGetValue("correlationId", out var correlation)
                ? correlation?.ToString()
                : null
        });

        return ValueTask.CompletedTask;
    }
}

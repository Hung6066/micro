using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.LabService.Domain.Entities;

public class CriticalAlertAuditEntry : Entity<Guid>
{
    public Guid CriticalAlertId { get; private set; }
    public string Action { get; private set; } = null!;
    public string ActorUserId { get; private set; } = null!;
    public string ActorDisplayName { get; private set; } = null!;
    public string? Notes { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private CriticalAlertAuditEntry(
        Guid id,
        Guid criticalAlertId,
        string action,
        string actorUserId,
        string actorDisplayName,
        string? notes)
        : base(id)
    {
        CriticalAlertId = criticalAlertId;
        Action = action;
        ActorUserId = actorUserId;
        ActorDisplayName = actorDisplayName;
        Notes = notes;
        OccurredAt = DateTime.UtcNow;
    }

    public static CriticalAlertAuditEntry Create(
        Guid criticalAlertId,
        string action,
        string actorUserId,
        string actorDisplayName,
        string? notes = null)
    {
        return new CriticalAlertAuditEntry(
            Guid.NewGuid(),
            criticalAlertId,
            action,
            actorUserId,
            actorDisplayName,
            notes);
    }

    private CriticalAlertAuditEntry() { }
}

using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.ValueObjects;

namespace His.Hope.LabService.Domain.Entities;

public class CriticalAlert : Entity<Guid>
{
    private readonly List<CriticalAlertAuditEntry> _auditEntries = [];

    public Guid LabOrderId { get; private set; }
    public Guid LabTestId { get; private set; }
    public Guid LabResultId { get; private set; }
    public Guid? RuleId { get; private set; }
    public CriticalAlertTriggerType TriggerType { get; private set; } = null!;
    public CriticalAlertStatus Status { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public string ResultValue { get; private set; } = null!;
    public string? ResultUnit { get; private set; }
    public decimal? ThresholdValue { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }
    public string? AcknowledgedByUserId { get; private set; }
    public string? AcknowledgedByDisplayName { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string? ResolvedByUserId { get; private set; }
    public string? ResolvedByDisplayName { get; private set; }
    public IReadOnlyCollection<CriticalAlertAuditEntry> AuditEntries => _auditEntries.AsReadOnly();

    private CriticalAlert(
        Guid id,
        Guid labOrderId,
        Guid labTestId,
        Guid labResultId,
        Guid? ruleId,
        CriticalAlertTriggerType triggerType,
        string message,
        string resultValue,
        string? resultUnit,
        decimal? thresholdValue,
        string actorUserId,
        string actorDisplayName)
        : base(id)
    {
        LabOrderId = labOrderId;
        LabTestId = labTestId;
        LabResultId = labResultId;
        RuleId = ruleId;
        TriggerType = triggerType;
        Status = CriticalAlertStatus.Open;
        Message = Guard.Against.NullOrWhiteSpace(message, nameof(message));
        ResultValue = Guard.Against.NullOrWhiteSpace(resultValue, nameof(resultValue));
        ResultUnit = resultUnit;
        ThresholdValue = thresholdValue;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;

        AddAuditEntry("Created", actorUserId, actorDisplayName);
    }

    public static CriticalAlert Create(
        Guid labOrderId,
        Guid labTestId,
        Guid labResultId,
        Guid? ruleId,
        CriticalAlertTriggerType triggerType,
        string message,
        string resultValue,
        string? resultUnit,
        decimal? thresholdValue,
        string actorUserId,
        string actorDisplayName)
    {
        return new CriticalAlert(
            Guid.NewGuid(),
            labOrderId,
            labTestId,
            labResultId,
            ruleId,
            triggerType,
            message,
            resultValue,
            resultUnit,
            thresholdValue,
            actorUserId,
            actorDisplayName);
    }

    public void UpdateObservation(
        Guid labResultId,
        Guid? ruleId,
        CriticalAlertTriggerType triggerType,
        string message,
        string resultValue,
        string? resultUnit,
        decimal? thresholdValue,
        string actorUserId,
        string actorDisplayName)
    {
        LabResultId = labResultId;
        RuleId = ruleId;
        TriggerType = triggerType;
        Message = Guard.Against.NullOrWhiteSpace(message, nameof(message));
        ResultValue = Guard.Against.NullOrWhiteSpace(resultValue, nameof(resultValue));
        ResultUnit = resultUnit;
        ThresholdValue = thresholdValue;
        UpdatedAt = DateTime.UtcNow;

        AddAuditEntry("Updated", actorUserId, actorDisplayName);
    }

    public void Acknowledge(string actorUserId, string actorDisplayName, string? notes = null)
    {
        if (Status == CriticalAlertStatus.Resolved)
            throw new InvalidOperationException("Cannot acknowledge a resolved critical alert.");

        Status = CriticalAlertStatus.Acknowledged;
        AcknowledgedAt = DateTime.UtcNow;
        AcknowledgedByUserId = Guard.Against.NullOrWhiteSpace(actorUserId, nameof(actorUserId));
        AcknowledgedByDisplayName = Guard.Against.NullOrWhiteSpace(actorDisplayName, nameof(actorDisplayName));
        UpdatedAt = AcknowledgedAt.Value;

        AddAuditEntry("Acknowledged", actorUserId, actorDisplayName, notes);
    }

    public void Resolve(string actorUserId, string actorDisplayName, string? notes = null)
    {
        if (Status == CriticalAlertStatus.Resolved)
            return;

        Status = CriticalAlertStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
        ResolvedByUserId = Guard.Against.NullOrWhiteSpace(actorUserId, nameof(actorUserId));
        ResolvedByDisplayName = Guard.Against.NullOrWhiteSpace(actorDisplayName, nameof(actorDisplayName));
        UpdatedAt = ResolvedAt.Value;

        AddAuditEntry("Resolved", actorUserId, actorDisplayName, notes);
    }

    private void AddAuditEntry(string action, string actorUserId, string actorDisplayName, string? notes = null)
    {
        _auditEntries.Add(CriticalAlertAuditEntry.Create(Id, action, actorUserId, actorDisplayName, notes));
    }

    private CriticalAlert() { }
}

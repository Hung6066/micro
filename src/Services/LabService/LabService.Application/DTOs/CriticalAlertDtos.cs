using His.Hope.LabService.Domain.ValueObjects;

namespace His.Hope.LabService.Application.DTOs;

public record CriticalAlertRuleDto(
    Guid Id,
    string TestCode,
    string TestName,
    string? Unit,
    decimal? LowCriticalValue,
    decimal? HighCriticalValue,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string CreatedByUserId,
    string CreatedByDisplayName);

public record CriticalAlertAuditEntryDto(
    Guid Id,
    Guid CriticalAlertId,
    string Action,
    string ActorUserId,
    string ActorDisplayName,
    string? Notes,
    DateTime OccurredAt);

public record CriticalAlertDto(
    Guid Id,
    Guid LabOrderId,
    Guid LabTestId,
    Guid LabResultId,
    Guid? RuleId,
    CriticalAlertTriggerType TriggerType,
    CriticalAlertStatus Status,
    string Message,
    string ResultValue,
    string? ResultUnit,
    decimal? ThresholdValue,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? AcknowledgedAt,
    string? AcknowledgedByUserId,
    string? AcknowledgedByDisplayName,
    DateTime? ResolvedAt,
    string? ResolvedByUserId,
    string? ResolvedByDisplayName,
    IReadOnlyCollection<CriticalAlertAuditEntryDto> AuditEntries);

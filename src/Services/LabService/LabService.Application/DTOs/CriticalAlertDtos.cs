using His.Hope.LabService.Domain.Entities;
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

internal static class CriticalAlertDtoMapper
{
    public static CriticalAlertRuleDto ToRuleDto(CriticalAlertRule rule) =>
        new(
            rule.Id,
            rule.TestCode,
            rule.TestName,
            rule.Unit,
            rule.LowCriticalValue,
            rule.HighCriticalValue,
            rule.IsActive,
            rule.CreatedAt,
            rule.UpdatedAt,
            rule.CreatedByUserId,
            rule.CreatedByDisplayName);

    public static CriticalAlertDto ToDto(CriticalAlert alert) =>
        new(
            alert.Id,
            alert.LabOrderId,
            alert.LabTestId,
            alert.LabResultId,
            alert.RuleId,
            alert.TriggerType,
            alert.Status,
            alert.Message,
            alert.ResultValue,
            alert.ResultUnit,
            alert.ThresholdValue,
            alert.CreatedAt,
            alert.UpdatedAt,
            alert.AcknowledgedAt,
            alert.AcknowledgedByUserId,
            alert.AcknowledgedByDisplayName,
            alert.ResolvedAt,
            alert.ResolvedByUserId,
            alert.ResolvedByDisplayName,
            alert.AuditEntries.Select(e => new CriticalAlertAuditEntryDto(
                e.Id,
                e.CriticalAlertId,
                e.Action,
                e.ActorUserId,
                e.ActorDisplayName,
                e.Notes,
                e.OccurredAt)).ToList());
}

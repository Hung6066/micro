using System.Globalization;
using His.Hope.LabService.Application.Common.Abstractions;
using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.ValueObjects;

namespace His.Hope.LabService.Application.Services;

public class CriticalAlertEvaluator
{
    private const string SystemActorUserId = "system";
    private const string SystemActorDisplayName = "System";

    private readonly ICriticalAlertRuleRepository _ruleRepository;
    private readonly ICriticalAlertRepository _alertRepository;
    private readonly ICurrentUserContext _currentUserContext;

    public CriticalAlertEvaluator(
        ICriticalAlertRuleRepository ruleRepository,
        ICriticalAlertRepository alertRepository,
        ICurrentUserContext currentUserContext)
    {
        _ruleRepository = ruleRepository;
        _alertRepository = alertRepository;
        _currentUserContext = currentUserContext;
    }

    public async Task<CriticalAlertDto?> EvaluateAsync(
        LabOrder order,
        LabTest test,
        LabResult result,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = GetActorUserId();
        var actorDisplayName = GetActorDisplayName();
        var isCriticalFlag = result.AbnormalFlag is not null &&
            (result.AbnormalFlag == AbnormalFlag.CriticalHigh || result.AbnormalFlag == AbnormalFlag.CriticalLow);
        var numericValue = TryParseDecimal(result.Value);
        var matchedRule = numericValue.HasValue
            ? await FindMatchingRuleAsync(test, result.Unit, numericValue.Value, cancellationToken)
            : null;
        var thresholdMatch = matchedRule is not null && numericValue.HasValue;
        var currentAlert = await _alertRepository.GetCurrentAsync(order.Id.Value, test.Id.Value, cancellationToken);

        if (!isCriticalFlag && !thresholdMatch)
            return await ResolveAsync(order, test, result, cancellationToken);

        var triggerType = GetTriggerType(isCriticalFlag, thresholdMatch);
        var thresholdValue = thresholdMatch ? matchedRule!.GetMatchedThreshold(numericValue!.Value) : null;
        var message = BuildMessage(test, result, isCriticalFlag, matchedRule, thresholdValue);

        if (currentAlert is null)
        {
            var alert = CriticalAlert.Create(
                order.Id.Value,
                test.Id.Value,
                result.LabResultId.Value,
                matchedRule?.Id,
                triggerType,
                message,
                result.Value,
                result.Unit,
                thresholdValue,
                actorUserId,
                actorDisplayName);

            await _alertRepository.AddAsync(alert, cancellationToken);
            await _alertRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            return ToDto(alert);
        }

        currentAlert.UpdateObservation(
            result.LabResultId.Value,
            matchedRule?.Id,
            triggerType,
            message,
            result.Value,
            result.Unit,
            thresholdValue,
            actorUserId,
            actorDisplayName);

        await _alertRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(currentAlert);
    }

    public async Task<CriticalAlertDto?> ResolveAsync(
        LabOrder order,
        LabTest test,
        LabResult result,
        CancellationToken cancellationToken = default)
    {
        var currentAlert = await _alertRepository.GetCurrentAsync(order.Id.Value, test.Id.Value, cancellationToken);
        if (currentAlert is null)
            return null;

        var actorUserId = GetActorUserId();
        var actorDisplayName = GetActorDisplayName();

        currentAlert.Resolve(actorUserId, actorDisplayName, $"Resolved by noncritical result {result.Value}");
        await _alertRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(currentAlert);
    }

    private async Task<CriticalAlertRule?> FindMatchingRuleAsync(LabTest test, string? resultUnit, decimal value, CancellationToken cancellationToken)
    {
        var rules = await _ruleRepository.ListActiveByTestCodeAsync(test.TestCode, cancellationToken);

        return rules.FirstOrDefault(rule => rule.Matches(resultUnit, value));
    }

    private static CriticalAlertTriggerType GetTriggerType(bool criticalFlag, bool thresholdMatch) =>
        criticalFlag && thresholdMatch
            ? CriticalAlertTriggerType.Both
            : criticalFlag
                ? CriticalAlertTriggerType.CriticalFlag
                : CriticalAlertTriggerType.Threshold;

    private static string BuildMessage(
        LabTest test,
        LabResult result,
        bool criticalFlag,
        CriticalAlertRule? matchedRule,
        decimal? thresholdValue)
    {
        var parts = new List<string>();

        if (criticalFlag && result.AbnormalFlag is not null)
            parts.Add($"Critical flag {result.AbnormalFlag.Code}");

        if (matchedRule is not null)
        {
            var thresholdText = thresholdValue?.ToString(CultureInfo.InvariantCulture) ?? "threshold";
            parts.Add($"{test.TestCode} result {result.Value} breached critical {thresholdText}");
        }

        return parts.Count == 0
            ? $"Critical lab result recorded for {test.TestCode}"
            : string.Join("; ", parts);
    }

    private string GetActorUserId() =>
        _currentUserContext.IsAuthenticated && !string.IsNullOrWhiteSpace(_currentUserContext.UserId)
            ? _currentUserContext.UserId
            : SystemActorUserId;

    private string GetActorDisplayName() =>
        _currentUserContext.IsAuthenticated && !string.IsNullOrWhiteSpace(_currentUserContext.FullName)
            ? _currentUserContext.FullName
            : SystemActorDisplayName;

    private static decimal? TryParseDecimal(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
            return parsed;

        return null;
    }

    private static CriticalAlertDto ToDto(CriticalAlert alert) =>
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

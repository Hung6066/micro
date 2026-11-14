namespace His.Hope.Contracts.Lab;

public sealed record CreateLabOrderRequest(
    Guid PatientId,
    Guid ProviderId,
    Guid? EncounterId,
    string PriorityCode,
    string? Notes,
    IReadOnlyList<CreateTestItemRequest> Tests);

public sealed record CreateTestItemRequest(
    string TestCode,
    string TestName,
    string? SpecimenType);

public sealed record RecordLabResultRequest(
    Guid TestId,
    string Value,
    string? AbnormalFlagCode,
    string? Notes);

public sealed record CancelLabOrderRequest(string Reason);

public sealed record CriticalAlertRuleUpsertRequest(
    string TestCode,
    string TestName,
    string? Unit,
    decimal? LowCriticalValue,
    decimal? HighCriticalValue,
    bool IsActive = true);

namespace His.Hope.Contracts.Clinical;

public sealed record StartEncounterRequest(
    Guid PatientId,
    Guid ProviderId,
    Guid? AppointmentId,
    string EncounterTypeCode);

public sealed record RecordVitalsRequest(
    decimal? Temperature,
    int? HeartRate,
    int? RespiratoryRate,
    int? SystolicBP,
    int? DiastolicBP,
    decimal? OxygenSaturation,
    decimal? HeightCm,
    decimal? WeightKg,
    decimal? Bmi);

public sealed record AddDiagnosisRequest(
    string ConditionName,
    string Icd10Code,
    bool IsPrimary,
    string? Notes);

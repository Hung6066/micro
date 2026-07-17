using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.ClinicalService.Domain.ValueObjects;

public class EncounterType : Enumeration<EncounterType>
{
    public static readonly EncounterType Outpatient = new("OP", "Outpatient");
    public static readonly EncounterType Inpatient = new("IP", "Inpatient");
    public static readonly EncounterType Emergency = new("ER", "Emergency");
    public static readonly EncounterType Telehealth = new("TH", "Telehealth");
    public static readonly EncounterType FollowUp = new("FU", "Follow-up");
    public static readonly EncounterType AnnualWellness = new("AW", "Annual Wellness");
    private EncounterType(string code, string name) : base(code, name) { }
}

public class EncounterStatus : Enumeration<EncounterStatus>
{
    public static readonly EncounterStatus InProgress = new("IN_PROGRESS", "In Progress");
    public static readonly EncounterStatus Completed = new("COMPLETED", "Completed");
    public static readonly EncounterStatus Signed = new("SIGNED", "Signed");
    private EncounterStatus(string code, string name) : base(code, name) { }
}

public class HistoryPresentIllness : ValueObject
{
    public string? Onset { get; }
    public string? Location { get; }
    public string? Duration { get; }
    public string? Characteristics { get; }
    public string? AggravatingFactors { get; }
    public string? RelievingFactors { get; }
    public string? PriorTreatments { get; }

    public HistoryPresentIllness(string? onset, string? location, string? duration,
        string? characteristics, string? aggravatingFactors, string? relievingFactors, string? priorTreatments)
    {
        Onset = onset; Location = location; Duration = duration;
        Characteristics = characteristics; AggravatingFactors = aggravatingFactors;
        RelievingFactors = relievingFactors; PriorTreatments = priorTreatments;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Onset ?? string.Empty; yield return Location ?? string.Empty;
        yield return Duration ?? string.Empty; yield return Characteristics ?? string.Empty;
        yield return AggravatingFactors ?? string.Empty; yield return RelievingFactors ?? string.Empty;
        yield return PriorTreatments ?? string.Empty;
    }
}

public class VitalSigns : ValueObject
{
    public decimal? Temperature { get; }
    public int? HeartRate { get; }
    public int? RespiratoryRate { get; }
    public int? SystolicBP { get; }
    public int? DiastolicBP { get; }
    public decimal? OxygenSaturation { get; }
    public decimal? HeightCm { get; }
    public decimal? WeightKg { get; }
    public decimal? Bmi { get; }

    public VitalSigns(decimal? temperature, int? heartRate, int? respiratoryRate,
        int? systolicBP, int? diastolicBP, decimal? oxygenSaturation, decimal? heightCm,
        decimal? weightKg, decimal? bmi)
    {
        Temperature = temperature; HeartRate = heartRate; RespiratoryRate = respiratoryRate;
        SystolicBP = systolicBP; DiastolicBP = diastolicBP; OxygenSaturation = oxygenSaturation;
        HeightCm = heightCm; WeightKg = weightKg; Bmi = bmi;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Temperature ?? 0; yield return HeartRate ?? 0;
        yield return RespiratoryRate ?? 0; yield return SystolicBP ?? 0;
        yield return DiastolicBP ?? 0; yield return OxygenSaturation ?? 0;
        yield return HeightCm ?? 0; yield return WeightKg ?? 0; yield return Bmi ?? 0;
    }
}

public class Diagnosis : ValueObject
{
    public string ConditionName { get; }
    public string Icd10Code { get; }
    public bool IsPrimary { get; }
    public string? Notes { get; }

    public Diagnosis(string conditionName, string icd10Code, bool isPrimary, string? notes)
    {
        ConditionName = Guard.Against.NullOrWhiteSpace(conditionName, nameof(conditionName));
        Icd10Code = Guard.Against.NullOrWhiteSpace(icd10Code, nameof(icd10Code));
        IsPrimary = isPrimary; Notes = notes;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ConditionName; yield return Icd10Code; yield return IsPrimary;
        yield return Notes ?? string.Empty;
    }
}

public class Procedure : ValueObject
{
    public string ProcedureName { get; }
    public string CptCode { get; }
    public DateTime PerformedDate { get; }
    public string? Notes { get; }

    public Procedure(string procedureName, string cptCode, DateTime performedDate, string? notes)
    {
        ProcedureName = Guard.Against.NullOrWhiteSpace(procedureName, nameof(procedureName));
        CptCode = Guard.Against.NullOrWhiteSpace(cptCode, nameof(cptCode));
        PerformedDate = performedDate; Notes = notes;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ProcedureName; yield return CptCode;
        yield return PerformedDate; yield return Notes ?? string.Empty;
    }
}

namespace His.Hope.ClinicalService.Application.DTOs;

public class EncounterDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid ProviderId { get; set; }
    public Guid? AppointmentId { get; set; }
    public DateTime EncounterDate { get; set; }
    public string EncounterTypeCode { get; set; } = string.Empty;
    public string EncounterTypeName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string? ChiefComplaint { get; set; }
    public string? Assessment { get; set; }
    public string? Plan { get; set; }
    public string? DiagnosisNotes { get; set; }
    public HpiDto? Hpi { get; set; }
    public VitalSignsDto? VitalSigns { get; set; }
    public List<DiagnosisDto> Diagnoses { get; set; } = [];
    public List<ProcedureDto> Procedures { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class VitalSignsDto
{
    public decimal? Temperature { get; set; }
    public int? HeartRate { get; set; }
    public int? RespiratoryRate { get; set; }
    public int? SystolicBP { get; set; }
    public int? DiastolicBP { get; set; }
    public decimal? OxygenSaturation { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? Bmi { get; set; }
}

public class HpiDto
{
    public string? Onset { get; set; }
    public string? Location { get; set; }
    public string? Duration { get; set; }
    public string? Characteristics { get; set; }
    public string? AggravatingFactors { get; set; }
    public string? RelievingFactors { get; set; }
    public string? PriorTreatments { get; set; }
}

public class DiagnosisDto
{
    public string ConditionName { get; set; } = string.Empty;
    public string Icd10Code { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public string? Notes { get; set; }
}

public class ProcedureDto
{
    public string ProcedureName { get; set; } = string.Empty;
    public string CptCode { get; set; } = string.Empty;
    public DateTime PerformedDate { get; set; }
    public string? Notes { get; set; }
}

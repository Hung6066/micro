using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.ClinicalService.Domain.Aggregates;

public class Encounter : AggregateRoot<EncounterId>
{
    public Guid PatientId { get; private set; }
    public Guid ProviderId { get; private set; }
    public Guid? AppointmentId { get; private set; }
    public DateTime EncounterDate { get; private set; }
    public EncounterType EncounterType { get; private set; }
    public string? ChiefComplaint { get; private set; }
    public HistoryPresentIllness? Hpi { get; private set; }
    public VitalSigns? VitalSigns { get; private set; }
    public string? Assessment { get; private set; }
    public string? Plan { get; private set; }
    public string? DiagnosisNotes { get; private set; }
    public EncounterStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<Diagnosis> _diagnoses = [];
    public IReadOnlyCollection<Diagnosis> Diagnoses => _diagnoses.AsReadOnly();

    private readonly List<Procedure> _procedures = [];
    public IReadOnlyCollection<Procedure> Procedures => _procedures.AsReadOnly();

    private Encounter(
        EncounterId id, Guid patientId, Guid providerId, EncounterType type)
        : base(id)
    {
        PatientId = patientId;
        ProviderId = providerId;
        EncounterType = type;
        EncounterDate = DateTime.UtcNow;
        Status = EncounterStatus.InProgress;
        CreatedAt = DateTime.UtcNow;
    }

    public static Encounter Start(Guid patientId, Guid providerId, EncounterType type) =>
        new(EncounterId.New(), patientId, providerId, type);

    public void RecordHpi(string? onset, string? location, string? duration,
        string? characteristics, string? aggravating, string? relieving,
        string? treatments)
    {
        Hpi = new HistoryPresentIllness(onset, location, duration,
            characteristics, aggravating, relieving, treatments);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordVitals(decimal? temperature, int? heartRate, int? respiratoryRate,
        int? systolicBp, int? diastolicBp, decimal? oxygenSaturation, decimal? height,
        decimal? weight, decimal? bmi)
    {
        VitalSigns = new VitalSigns(temperature, heartRate, respiratoryRate,
            systolicBp, diastolicBp, oxygenSaturation, height, weight, bmi);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddDiagnosis(Diagnosis diagnosis)
    {
        _diagnoses.Add(diagnosis);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordAssessment(string assessment)
    {
        Assessment = assessment;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordPlan(string plan)
    {
        Plan = plan;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        Status = EncounterStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    private Encounter() { }
}

using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PatientService.Domain.Entities;

public class MedicalCondition : Entity<Guid>
{
    public string ConditionName { get; private set; }
    public string? Icd10Code { get; private set; }
    public DateTime? OnsetDate { get; private set; }
    public DateTime? ResolvedDate { get; private set; }
    public bool IsChronic { get; private set; }
    public string? Notes { get; private set; }
    public DateTime RecordedDate { get; private set; }
    public bool IsActive { get; private set; }

    public MedicalCondition(
        string conditionName,
        string? icd10Code,
        DateTime? onsetDate,
        bool isChronic,
        string? notes)
        : base(Guid.NewGuid())
    {
        ConditionName = Guard.Against.NullOrWhiteSpace(conditionName, nameof(conditionName));
        Icd10Code = icd10Code;
        OnsetDate = onsetDate;
        IsChronic = isChronic;
        Notes = notes;
        RecordedDate = DateTime.UtcNow;
        IsActive = true;
    }

    public void Resolve(DateTime resolvedDate)
    {
        ResolvedDate = resolvedDate;
        IsActive = false;
    }

    private MedicalCondition() { }
}

using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.PatientService.Domain.Entities;

public class Allergy : Entity<Guid>
{
    public string Allergen { get; private set; }
    public string? Reaction { get; private set; }
    public string? Severity { get; private set; }
    public DateTime RecordedDate { get; private set; }
    public bool IsActive { get; private set; }

    public Allergy(string allergen, string? reaction, string? severity)
        : base(Guid.NewGuid())
    {
        Allergen = Guard.Against.NullOrWhiteSpace(allergen, nameof(allergen));
        Reaction = reaction;
        Severity = severity;
        RecordedDate = DateTime.UtcNow;
        IsActive = true;
    }

    public void MarkInactive()
    {
        IsActive = false;
    }

    private Allergy() { }
}

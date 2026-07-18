namespace His.Hope.PatientService.Infrastructure.Projections;

/// <summary>
/// CQRS read model for patient queries.
/// Denormalized and optimized for fast reads — updated by <see cref="PatientProjector"/>
/// via integration events from the write side.
/// </summary>
public class PatientProjection
{
    public Guid PatientId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string? PrimaryDiagnosis { get; set; }
    public DateTime? LastVisitDate { get; set; }
    public int EncounterCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

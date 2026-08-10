using His.Hope.SharedKernel.Domain.Common;

namespace His.Hope.ClinicalService.Domain.Entities;

public class ClinicalNote : Entity<Guid>
{
    public Guid EncounterId { get; private set; }
    public string Content { get; private set; }
    public NoteType NoteType { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; }

    private ClinicalNote() : base() { Content = null!; NoteType = null!; CreatedBy = null!; }

    public ClinicalNote(
        Guid id,
        Guid encounterId,
        string content,
        NoteType noteType,
        string createdBy)
        : base(id)
    {
        EncounterId = encounterId;
        Content = Guard.Against.NullOrWhiteSpace(content, nameof(content));
        NoteType = noteType ?? throw new ArgumentNullException(nameof(noteType));
        CreatedAt = DateTime.UtcNow;
        CreatedBy = Guard.Against.NullOrWhiteSpace(createdBy, nameof(createdBy));
    }
}

public class NoteType : Enumeration<NoteType>
{
    public static readonly NoteType ProgressNote = new("PROGRESS", "Progress Note");
    public static readonly NoteType ConsultNote = new("CONSULT", "Consult Note");
    public static readonly NoteType DischargeSummary = new("DISCHARGE", "Discharge Summary");
    public static readonly NoteType OperativeReport = new("OPERATIVE", "Operative Report");
    public static readonly NoteType NursingNote = new("NURSING", "Nursing Note");
    public static readonly NoteType PhysicianNote = new("PHYSICIAN", "Physician Note");

    private NoteType(string code, string name) : base(code, name) { }
}

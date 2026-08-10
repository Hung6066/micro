using FluentAssertions;
using His.Hope.ClinicalService.Domain.Entities;

namespace His.Hope.ClinicalService.Domain.Tests;

public class ClinicalNoteTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldSetProperties()
    {
        var id = Guid.NewGuid();
        var encounterId = Guid.NewGuid();

        var note = new ClinicalNote(id, encounterId, "Patient presents with chest pain",
            NoteType.ProgressNote, "Dr. Smith");

        note.Id.Should().Be(id);
        note.EncounterId.Should().Be(encounterId);
        note.Content.Should().Be("Patient presents with chest pain");
        note.NoteType.Should().Be(NoteType.ProgressNote);
        note.CreatedBy.Should().Be("Dr. Smith");
        note.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_WithEmptyContent_ShouldThrow()
    {
        var act = () => new ClinicalNote(Guid.NewGuid(), Guid.NewGuid(), "",
            NoteType.ConsultNote, "Dr. Jones");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("content");
    }

    [Fact]
    public void Constructor_WithWhitespaceContent_ShouldThrow()
    {
        var act = () => new ClinicalNote(Guid.NewGuid(), Guid.NewGuid(), "   ",
            NoteType.ProgressNote, "Dr. Smith");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("content");
    }

    [Fact]
    public void Constructor_WithNullContent_ShouldThrow()
    {
        var act = () => new ClinicalNote(Guid.NewGuid(), Guid.NewGuid(), null!,
            NoteType.ProgressNote, "Dr. Smith");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("content");
    }

    [Fact]
    public void Constructor_WithEmptyCreatedBy_ShouldThrow()
    {
        var act = () => new ClinicalNote(Guid.NewGuid(), Guid.NewGuid(), "Content",
            NoteType.ProgressNote, "");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("createdBy");
    }

    [Fact]
    public void Constructor_WithNullNoteType_ShouldThrow()
    {
        var act = () => new ClinicalNote(Guid.NewGuid(), Guid.NewGuid(), "Content",
            null!, "Dr. Smith");

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("noteType");
    }

    [Fact]
    public void Constructor_WithDifferentNoteTypes_ShouldSetCorrectly()
    {
        var types = new[] { NoteType.ProgressNote, NoteType.ConsultNote, NoteType.DischargeSummary,
            NoteType.OperativeReport, NoteType.NursingNote, NoteType.PhysicianNote };

        foreach (var type in types)
        {
            var note = new ClinicalNote(Guid.NewGuid(), Guid.NewGuid(), "Content", type, "Dr. A");
            note.NoteType.Should().Be(type);
        }
    }

    [Fact]
    public void NoteType_AllValues_ShouldBeDefined()
    {
        var all = NoteType.GetAll();

        all.Should().HaveCount(6);
        all.Should().Contain(NoteType.ProgressNote);
        all.Should().Contain(NoteType.ConsultNote);
        all.Should().Contain(NoteType.DischargeSummary);
        all.Should().Contain(NoteType.OperativeReport);
        all.Should().Contain(NoteType.NursingNote);
        all.Should().Contain(NoteType.PhysicianNote);
    }

    [Theory]
    [InlineData("PROGRESS", "Progress Note")]
    [InlineData("CONSULT", "Consult Note")]
    [InlineData("DISCHARGE", "Discharge Summary")]
    [InlineData("OPERATIVE", "Operative Report")]
    [InlineData("NURSING", "Nursing Note")]
    [InlineData("PHYSICIAN", "Physician Note")]
    public void NoteType_FromCode_ShouldReturnCorrect(string code, string name)
    {
        var type = NoteType.FromCode(code);
        type.Code.Should().Be(code);
        type.Name.Should().Be(name);
    }

    [Fact]
    public void CreatedAt_ShouldBeUtc()
    {
        var note = new ClinicalNote(Guid.NewGuid(), Guid.NewGuid(), "Content",
            NoteType.ProgressNote, "Dr. Smith");

        note.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}

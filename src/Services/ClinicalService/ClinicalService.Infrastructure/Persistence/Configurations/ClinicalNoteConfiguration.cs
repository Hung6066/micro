using His.Hope.ClinicalService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.ClinicalService.Infrastructure.Persistence.Configurations;

public class ClinicalNoteConfiguration : IEntityTypeConfiguration<ClinicalNote>
{
    public void Configure(EntityTypeBuilder<ClinicalNote> builder)
    {
        builder.ToTable("ClinicalNotes");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.EncounterId)
            .IsRequired();

        builder.Property(e => e.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(e => e.NoteType)
            .IsRequired()
            .HasConversion(
                v => v.Code,
                v => Domain.Entities.NoteType.FromCode(v))
            .HasMaxLength(50);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(e => e.EncounterId);
    }
}

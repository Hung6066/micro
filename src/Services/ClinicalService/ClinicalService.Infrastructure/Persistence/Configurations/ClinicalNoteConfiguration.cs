using His.Hope.ClinicalService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.ClinicalService.Infrastructure.Persistence.Configurations;

public class ClinicalNoteConfiguration : IEntityTypeConfiguration<ClinicalNote>
{
    public void Configure(EntityTypeBuilder<ClinicalNote> builder)
    {
        builder.ToTable("clinical_notes");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("noteid")
            .ValueGeneratedNever();

        builder.Property(e => e.EncounterId)
            .HasColumnName("encounterid")
            .IsRequired();

        builder.Property(e => e.Content)
            .HasColumnName("content")
            .IsRequired()
            .HasColumnType("text");

        builder.Property(e => e.NoteType)
            .HasColumnName("notetype")
            .IsRequired()
            .HasConversion(
                v => v.Code,
                v => Domain.Entities.NoteType.FromCode(v))
            .HasMaxLength(50);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("createdat")
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .HasColumnName("createdby")
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(e => e.EncounterId);
    }
}

using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.PatientService.Infrastructure.Persistence.Configurations;

public class AllergyConfiguration : IEntityTypeConfiguration<Allergy>
{
    public void Configure(EntityTypeBuilder<Allergy> builder)
    {
        builder.ToTable("allergies");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("allergy_id");

        // The PatientId shadow property must use the same value converter as
        // Patient.Id (PatientId value object) to maintain type compatibility
        // with the principal key configured in PatientConfiguration.
        builder.Property<PatientId>("PatientId")
            .HasConversion(
                id => id.Value,
                value => PatientId.From(value))
            .HasColumnName("patient_id");

        builder.Property(a => a.Allergen).HasColumnName("allergen").HasMaxLength(200).IsRequired();
        builder.Property(a => a.Reaction).HasColumnName("reaction").HasMaxLength(500);
        builder.Property(a => a.Severity).HasColumnName("severity").HasMaxLength(50);
        builder.Property(a => a.RecordedDate).HasColumnName("recorded_date").IsRequired();
        builder.Property(a => a.IsActive).HasColumnName("is_active").IsRequired();
    }
}

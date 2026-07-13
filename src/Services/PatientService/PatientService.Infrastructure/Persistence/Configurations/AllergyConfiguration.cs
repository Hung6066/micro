using His.Hope.PatientService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.PatientService.Infrastructure.Persistence.Configurations;

public class AllergyConfiguration : IEntityTypeConfiguration<Allergy>
{
    public void Configure(EntityTypeBuilder<Allergy> builder)
    {
        builder.ToTable("Allergies");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Allergen).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Reaction).HasMaxLength(500);
        builder.Property(a => a.Severity).HasMaxLength(50);
        builder.Property(a => a.RecordedDate).IsRequired();
        builder.Property(a => a.IsActive).IsRequired();
    }
}

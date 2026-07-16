using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.PharmacyService.Infrastructure.Persistence.Configurations;

public class MedicationConfiguration : IEntityTypeConfiguration<Medication>
{
    public void Configure(EntityTypeBuilder<Medication> builder)
    {
        builder.ToTable("Medications");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasConversion(
                id => id.Value,
                value => MedicationId.From(value));

        builder.Property(m => m.Name).HasMaxLength(200).IsRequired();
        builder.Property(m => m.GenericName).HasMaxLength(200);
        builder.Property(m => m.BrandName).HasMaxLength(200);
        builder.Property(m => m.DosageForm).HasMaxLength(50).IsRequired();
        builder.Property(m => m.Strength).HasMaxLength(50).IsRequired();
        builder.Property(m => m.Route).HasMaxLength(50);
        builder.Property(m => m.Category).HasMaxLength(100);
        builder.Property(m => m.Manufacturer).HasMaxLength(200);
        builder.Property(m => m.RequiresPrescription).IsRequired();
        builder.Property(m => m.IsActive).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.UpdatedAt);

        builder.HasIndex(m => m.Name);
        builder.HasIndex(m => m.GenericName);
        builder.HasIndex(m => m.IsActive);
    }
}

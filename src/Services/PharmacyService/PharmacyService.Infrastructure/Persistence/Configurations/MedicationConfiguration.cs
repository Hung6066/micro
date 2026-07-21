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
                value => MedicationId.From(value))
            .HasColumnName("id");

        builder.Property(m => m.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(m => m.GenericName).HasColumnName("genericname").HasMaxLength(200);
        builder.Property(m => m.BrandName).HasColumnName("brandname").HasMaxLength(200);
        builder.Property(m => m.DosageForm).HasColumnName("dosageform").HasMaxLength(50).IsRequired();
        builder.Property(m => m.Strength).HasColumnName("strength").HasMaxLength(50).IsRequired();
        builder.Property(m => m.Route).HasColumnName("route").HasMaxLength(50);
        builder.Property(m => m.Category).HasColumnName("category").HasMaxLength(100);
        builder.Property(m => m.Manufacturer).HasColumnName("manufacturer").HasMaxLength(200);
        builder.Property(m => m.RequiresPrescription).HasColumnName("requiresprescription").IsRequired();
        builder.Property(m => m.IsActive).HasColumnName("isactive").IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnName("createdat").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnName("updatedat");

        builder.HasIndex(m => m.Name);
        builder.HasIndex(m => m.GenericName);
        builder.HasIndex(m => m.IsActive);
    }
}

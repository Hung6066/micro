using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.PharmacyService.Infrastructure.Persistence.Configurations;

public class MedicationConfiguration : IEntityTypeConfiguration<Medication>
{
    public void Configure(EntityTypeBuilder<Medication> builder)
    {
        builder.ToTable("medications");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasConversion(
                id => id.Value,
                value => MedicationId.From(value))
            .HasColumnName("id");

        builder.Property(m => m.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(m => m.GenericName).HasColumnName("generic_name").HasMaxLength(200);
        builder.Property(m => m.BrandName).HasColumnName("brand_name").HasMaxLength(200);
        builder.Property(m => m.DosageForm).HasColumnName("dosage_form").HasMaxLength(50).IsRequired();
        builder.Property(m => m.Strength).HasColumnName("strength").HasMaxLength(50).IsRequired();
        builder.Property(m => m.Route).HasColumnName("route").HasMaxLength(50);
        builder.Property(m => m.Category).HasColumnName("category").HasMaxLength(100);
        builder.Property(m => m.Manufacturer).HasColumnName("manufacturer").HasMaxLength(200);
        builder.Property(m => m.RequiresPrescription).HasColumnName("requires_prescription").IsRequired();
        builder.Property(m => m.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(m => m.Name);
        builder.HasIndex(m => m.GenericName);
        builder.HasIndex(m => m.IsActive);
    }
}

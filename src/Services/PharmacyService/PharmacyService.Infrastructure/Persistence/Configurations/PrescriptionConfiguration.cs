using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.PharmacyService.Infrastructure.Persistence.Configurations;

public class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("prescriptions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => PrescriptionId.From(value))
            .HasColumnName("id");

        builder.Property(p => p.PatientId).HasColumnName("patient_id").IsRequired();
        builder.Property(p => p.ProviderId).HasColumnName("provider_id").IsRequired();
        builder.Property(p => p.MedicationId).HasColumnName("medication_id");

        builder.Property(p => p.MedicationName).HasColumnName("medication_name").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Strength).HasColumnName("strength").HasMaxLength(50).IsRequired();
        builder.Property(p => p.DosageForm).HasColumnName("dosage_form").HasMaxLength(50).IsRequired();
        builder.Property(p => p.DosageInstructions).HasColumnName("dosage_instructions").HasMaxLength(500).IsRequired();
        builder.Property(p => p.Route).HasColumnName("route").HasMaxLength(50);

        builder.Property(p => p.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(p => p.Refills).HasColumnName("refills").IsRequired();
        builder.Property(p => p.Notes).HasColumnName("notes").HasMaxLength(1000);

        builder.Property(p => p.Status)
            .HasConversion(
                s => s.Code,
                code => PrescriptionStatus.FromCode(code))
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.PrescribedDate).HasColumnName("prescribed_date").IsRequired();
        builder.Property(p => p.ExpiryDate).HasColumnName("expiry_date");
        builder.Property(p => p.FilledDate).HasColumnName("filled_date");
        builder.Property(p => p.CancelledDate).HasColumnName("cancelled_date");
        builder.Property(p => p.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(500);
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(p => p.PatientId);
        builder.HasIndex(p => p.ProviderId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.PrescribedDate);
    }
}

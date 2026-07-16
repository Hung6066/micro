using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.PharmacyService.Infrastructure.Persistence.Configurations;

public class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("Prescriptions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => PrescriptionId.From(value));

        builder.Property(p => p.PatientId).IsRequired();
        builder.Property(p => p.ProviderId).IsRequired();
        builder.Property(p => p.MedicationId);

        builder.Property(p => p.MedicationName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Strength).HasMaxLength(50).IsRequired();
        builder.Property(p => p.DosageForm).HasMaxLength(50).IsRequired();
        builder.Property(p => p.DosageInstructions).HasMaxLength(500).IsRequired();
        builder.Property(p => p.Route).HasMaxLength(50);

        builder.Property(p => p.Quantity).IsRequired();
        builder.Property(p => p.Refills).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(1000);

        builder.Property(p => p.Status)
            .HasConversion(
                s => s.Code,
                code => PrescriptionStatus.FromCode(code))
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.PrescribedDate).IsRequired();
        builder.Property(p => p.ExpiryDate);
        builder.Property(p => p.FilledDate);
        builder.Property(p => p.CancelledDate);
        builder.Property(p => p.CancellationReason).HasMaxLength(500);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);

        builder.HasIndex(p => p.PatientId);
        builder.HasIndex(p => p.ProviderId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.PrescribedDate);
    }
}

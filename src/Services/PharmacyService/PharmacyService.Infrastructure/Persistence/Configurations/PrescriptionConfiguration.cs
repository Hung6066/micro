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
                value => PrescriptionId.From(value))
            .HasColumnName("id");

        builder.Property(p => p.PatientId).HasColumnName("patientid").IsRequired();
        builder.Property(p => p.ProviderId).HasColumnName("providerid").IsRequired();
        builder.Property(p => p.MedicationId).HasColumnName("medicationid");

        builder.Property(p => p.MedicationName).HasColumnName("medicationname").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Strength).HasColumnName("strength").HasMaxLength(50).IsRequired();
        builder.Property(p => p.DosageForm).HasColumnName("dosageform").HasMaxLength(50).IsRequired();
        builder.Property(p => p.DosageInstructions).HasColumnName("dosageinstructions").HasMaxLength(500).IsRequired();
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

        builder.Property(p => p.PrescribedDate).HasColumnName("prescribeddate").IsRequired();
        builder.Property(p => p.ExpiryDate).HasColumnName("expirydate");
        builder.Property(p => p.FilledDate).HasColumnName("filleddate");
        builder.Property(p => p.CancelledDate).HasColumnName("cancelleddate");
        builder.Property(p => p.CancellationReason).HasColumnName("cancellationreason").HasMaxLength(500);
        builder.Property(p => p.CreatedAt).HasColumnName("createdat").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updatedat");

        builder.HasIndex(p => p.PatientId);
        builder.HasIndex(p => p.ProviderId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.PrescribedDate);
    }
}

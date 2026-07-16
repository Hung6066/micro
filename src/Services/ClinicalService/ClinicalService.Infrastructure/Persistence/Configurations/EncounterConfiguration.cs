using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.ClinicalService.Infrastructure.Persistence.Configurations;

public class EncounterConfiguration : IEntityTypeConfiguration<Encounter>
{
    public void Configure(EntityTypeBuilder<Encounter> builder)
    {
        builder.ToTable("Encounters");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => EncounterId.From(value));

        builder.Property(e => e.PatientId).IsRequired();
        builder.Property(e => e.ProviderId).IsRequired();
        builder.Property(e => e.AppointmentId);
        builder.Property(e => e.EncounterDate).IsRequired();

        builder.Property(e => e.EncounterType)
            .HasConversion(
                t => t.Code,
                code => EncounterType.FromCode(code))
            .HasColumnName("EncounterType")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion(
                s => s.Code,
                code => EncounterStatus.FromCode(code))
            .HasColumnName("Status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ChiefComplaint).HasMaxLength(1000);
        builder.Property(e => e.Assessment).HasMaxLength(5000);
        builder.Property(e => e.Plan).HasMaxLength(5000);
        builder.Property(e => e.DiagnosisNotes).HasMaxLength(5000);

        builder.OwnsOne(e => e.Hpi, hpi =>
        {
            hpi.Property(h => h.Onset).HasColumnName("HpiOnset").HasMaxLength(500);
            hpi.Property(h => h.Location).HasColumnName("HpiLocation").HasMaxLength(500);
            hpi.Property(h => h.Duration).HasColumnName("HpiDuration").HasMaxLength(200);
            hpi.Property(h => h.Characteristics).HasColumnName("HpiCharacteristics").HasMaxLength(1000);
            hpi.Property(h => h.AggravatingFactors).HasColumnName("HpiAggravatingFactors").HasMaxLength(1000);
            hpi.Property(h => h.RelievingFactors).HasColumnName("HpiRelievingFactors").HasMaxLength(1000);
            hpi.Property(h => h.PriorTreatments).HasColumnName("HpiPriorTreatments").HasMaxLength(1000);
        });

        builder.OwnsOne(e => e.VitalSigns, vs =>
        {
            vs.Property(v => v.Temperature).HasColumnName("Temperature").HasPrecision(5, 2);
            vs.Property(v => v.HeartRate).HasColumnName("HeartRate");
            vs.Property(v => v.RespiratoryRate).HasColumnName("RespiratoryRate");
            vs.Property(v => v.SystolicBP).HasColumnName("SystolicBP");
            vs.Property(v => v.DiastolicBP).HasColumnName("DiastolicBP");
            vs.Property(v => v.OxygenSaturation).HasColumnName("OxygenSaturation").HasPrecision(5, 2);
            vs.Property(v => v.HeightCm).HasColumnName("HeightCm").HasPrecision(6, 2);
            vs.Property(v => v.WeightKg).HasColumnName("WeightKg").HasPrecision(6, 2);
            vs.Property(v => v.Bmi).HasColumnName("Bmi").HasPrecision(5, 2);
        });

        builder.OwnsMany(e => e.Diagnoses, d =>
        {
            d.WithOwner().HasForeignKey("EncounterId");
            d.ToTable("EncounterDiagnoses");
            d.Property(diag => diag.ConditionName).HasColumnName("ConditionName").HasMaxLength(500).IsRequired();
            d.Property(diag => diag.Icd10Code).HasColumnName("Icd10Code").HasMaxLength(20).IsRequired();
            d.Property(diag => diag.IsPrimary).HasColumnName("IsPrimary").IsRequired();
            d.Property(diag => diag.Notes).HasColumnName("Notes").HasMaxLength(1000);
        });

        builder.OwnsMany(e => e.Procedures, p =>
        {
            p.WithOwner().HasForeignKey("EncounterId");
            p.ToTable("EncounterProcedures");
            p.Property(proc => proc.ProcedureName).HasColumnName("ProcedureName").HasMaxLength(500).IsRequired();
            p.Property(proc => proc.CptCode).HasColumnName("CptCode").HasMaxLength(20).IsRequired();
            p.Property(proc => proc.PerformedDate).HasColumnName("PerformedDate").IsRequired();
            p.Property(proc => proc.Notes).HasColumnName("Notes").HasMaxLength(1000);
        });

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        builder.HasIndex(e => e.PatientId);
        builder.HasIndex(e => e.ProviderId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.EncounterDate);
    }
}

using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace His.Hope.ClinicalService.Infrastructure.Persistence.Configurations;

public class EncounterConfiguration : IEntityTypeConfiguration<Encounter>
{
    public void Configure(EntityTypeBuilder<Encounter> builder)
    {
        builder.ToTable("encounters");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("encounter_id")
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
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion(
                s => s.Code,
                code => EncounterStatus.FromCode(code))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ChiefComplaint).HasMaxLength(1000);
        builder.Property(e => e.Assessment).HasMaxLength(5000);
        builder.Property(e => e.Plan).HasMaxLength(5000);
        builder.Property(e => e.DiagnosisNotes).HasMaxLength(5000);

        builder.OwnsOne(e => e.Hpi, hpi =>
        {
            hpi.Property(h => h.Onset).HasColumnName("hpi_onset").HasMaxLength(500);
            hpi.Property(h => h.Location).HasColumnName("hpi_location").HasMaxLength(500);
            hpi.Property(h => h.Duration).HasColumnName("hpi_duration").HasMaxLength(200);
            hpi.Property(h => h.Characteristics).HasColumnName("hpi_characteristics").HasMaxLength(1000);
            hpi.Property(h => h.AggravatingFactors).HasColumnName("hpi_aggravating_factors").HasMaxLength(1000);
            hpi.Property(h => h.RelievingFactors).HasColumnName("hpi_relieving_factors").HasMaxLength(1000);
            hpi.Property(h => h.PriorTreatments).HasColumnName("hpi_prior_treatments").HasMaxLength(1000);
        });

        builder.OwnsOne(e => e.VitalSigns, vs =>
        {
            vs.Property(v => v.Temperature).HasColumnName("temperature").HasPrecision(5, 2);
            vs.Property(v => v.HeartRate).HasColumnName("heart_rate");
            vs.Property(v => v.RespiratoryRate).HasColumnName("respiratory_rate");
            vs.Property(v => v.SystolicBP).HasColumnName("systolic_bp");
            vs.Property(v => v.DiastolicBP).HasColumnName("diastolic_bp");
            vs.Property(v => v.OxygenSaturation).HasColumnName("oxygen_saturation").HasPrecision(5, 2);
            vs.Property(v => v.HeightCm).HasColumnName("height_cm").HasPrecision(6, 2);
            vs.Property(v => v.WeightKg).HasColumnName("weight_kg").HasPrecision(6, 2);
            vs.Property(v => v.Bmi).HasColumnName("bmi").HasPrecision(5, 2);
        });

        builder.OwnsMany(e => e.Diagnoses, d =>
        {
            d.WithOwner().HasForeignKey("encounter_id");
            d.ToTable("encounter_diagnoses");
            d.Property(diag => diag.ConditionName).HasColumnName("condition_name").HasMaxLength(500).IsRequired();
            d.Property(diag => diag.Icd10Code).HasColumnName("icd10_code").HasMaxLength(20).IsRequired();
            d.Property(diag => diag.IsPrimary).HasColumnName("is_primary").IsRequired();
            d.Property(diag => diag.Notes).HasColumnName("notes").HasMaxLength(1000);
        });

        builder.OwnsMany(e => e.Procedures, p =>
        {
            p.WithOwner().HasForeignKey("encounter_id");
            p.ToTable("encounter_procedures");
            p.Property(proc => proc.ProcedureName).HasColumnName("procedure_name").HasMaxLength(500).IsRequired();
            p.Property(proc => proc.CptCode).HasColumnName("cpt_code").HasMaxLength(20).IsRequired();
            p.Property(proc => proc.PerformedDate).HasColumnName("performed_date").IsRequired();
            p.Property(proc => proc.Notes).HasColumnName("notes").HasMaxLength(1000);
        });

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.PatientId).HasDatabaseName("ix_encounters_patientid");
        builder.HasIndex(e => e.ProviderId).HasDatabaseName("ix_encounters_providerid");
        builder.HasIndex(e => e.Status).HasDatabaseName("ix_encounters_status");
        builder.HasIndex(e => e.EncounterDate).HasDatabaseName("ix_encounters_encounterdate");
    }
}

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

        builder.Property(e => e.FacilityId).HasColumnName("facility_id").HasMaxLength(100);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("encounter_id")
            .HasConversion(
                id => id.Value,
                value => EncounterId.From(value));

        // The production database was created by the initial migration with
        // quoted PascalCase identifiers. Keep the mapping explicit so current
        // EF/Npgsql conventions do not look for snake_case columns.
        builder.Property(e => e.PatientId).HasColumnName("PatientId").IsRequired();
        builder.Property(e => e.ProviderId).HasColumnName("ProviderId").IsRequired();
        builder.Property(e => e.AppointmentId).HasColumnName("AppointmentId");
        builder.Property(e => e.EncounterDate).HasColumnName("EncounterDate").IsRequired();

        builder.Property(e => e.EncounterType)
            .HasColumnName("EncounterType")
            .HasConversion(
                t => t.Code,
                code => EncounterType.FromCode(code))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("Status")
            .HasConversion(
                s => s.Code,
                code => EncounterStatus.FromCode(code))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ChiefComplaint).HasColumnName("ChiefComplaint").HasMaxLength(1000);
        builder.Property(e => e.Assessment).HasColumnName("Assessment").HasMaxLength(5000);
        builder.Property(e => e.Plan).HasColumnName("Plan").HasMaxLength(5000);
        builder.Property(e => e.DiagnosisNotes).HasColumnName("DiagnosisNotes").HasMaxLength(5000);

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
            d.Property<int>("Id").HasColumnName("Id");
            d.Property(diag => diag.ConditionName).HasColumnName("condition_name").HasMaxLength(500).IsRequired();
            d.Property(diag => diag.Icd10Code).HasColumnName("icd10_code").HasMaxLength(20).IsRequired();
            d.Property(diag => diag.IsPrimary).HasColumnName("is_primary").IsRequired();
            d.Property(diag => diag.Notes).HasColumnName("notes").HasMaxLength(1000);
        });

        builder.OwnsMany(e => e.Procedures, p =>
        {
            p.WithOwner().HasForeignKey("encounter_id");
            p.ToTable("encounter_procedures");
            p.Property<int>("Id").HasColumnName("Id");
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
        builder.HasIndex(e => e.FacilityId).HasDatabaseName("ix_encounters_facility_id");
        builder.HasIndex(e => new { e.PatientId, e.EncounterDate })
            .HasDatabaseName("ix_encounters_patient_date_id");
        builder.HasIndex(e => new { e.ProviderId, e.EncounterDate })
            .HasDatabaseName("ix_encounters_provider_date_id");
        builder.HasIndex(e => new { e.FacilityId, e.Status, e.EncounterDate })
            .HasDatabaseName("ix_encounters_facility_status_date_id");
    }
}

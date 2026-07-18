using Microsoft.EntityFrameworkCore;

namespace His.Hope.PatientService.Infrastructure.Projections;

/// <summary>
/// Read-only DbContext for CQRS read-side patient projections.
/// Configured with no tracking by default and query splitting for performant reads.
/// </summary>
public class PatientReadDbContext : DbContext
{
    public DbSet<PatientProjection> PatientProjections => Set<PatientProjection>();

    public PatientReadDbContext(DbContextOptions<PatientReadDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PatientProjection>(entity =>
        {
            entity.ToTable("patient_read_models");

            entity.HasKey(e => e.PatientId).HasName("pk_patient_read_models");

            entity.Property(e => e.PatientId)
                .HasColumnName("patient_id")
                .ValueGeneratedNever();

            entity.Property(e => e.FullName)
                .HasColumnName("full_name")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.DateOfBirth)
                .HasColumnName("date_of_birth")
                .IsRequired();

            entity.Property(e => e.Gender)
                .HasColumnName("gender")
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(e => e.PrimaryDiagnosis)
                .HasColumnName("primary_diagnosis")
                .HasMaxLength(500);

            entity.Property(e => e.LastVisitDate)
                .HasColumnName("last_visit_date");

            entity.Property(e => e.EncounterCount)
                .HasColumnName("encounter_count")
                .HasDefaultValue(0);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.HasIndex(e => e.LastVisitDate)
                .HasDatabaseName("ix_patient_read_models_last_visit_date");

            entity.HasIndex(e => e.FullName)
                .HasDatabaseName("ix_patient_read_models_full_name");
        });

        base.OnModelCreating(modelBuilder);
    }
}

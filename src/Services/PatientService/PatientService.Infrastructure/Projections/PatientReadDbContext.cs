using His.Hope.Authorization;
using His.Hope.Infrastructure.DataLifecycle;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.PatientService.Infrastructure.Projections;

/// <summary>
/// Read-only DbContext for CQRS read-side patient projections.
/// Configured with no tracking by default and query splitting for performant reads.
/// </summary>
public class PatientReadDbContext : DbContext
{
    private readonly FacilityAccessScope _facilityScope;

    public DbSet<PatientProjection> PatientProjections => Set<PatientProjection>();

    public PatientReadDbContext(
        DbContextOptions<PatientReadDbContext> options,
        IHttpContextAccessor? httpContextAccessor = null)
        : base(options)
    {
        _facilityScope = FacilityScopeEfExtensions.Resolve(httpContextAccessor);
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PatientProjection>(entity =>
        {
            entity.ToTable("patient_read_models");

            entity.HasKey(e => e.PatientId).HasName("pk_patient_read_models");

            entity.Property(e => e.FacilityId)
                .HasColumnName("facility_id")
                .HasMaxLength(100);

            entity.HasIndex(e => e.FacilityId)
                .HasDatabaseName("ix_patient_read_models_facility_id");

            entity.HasQueryFilter(projection =>
                EF.Property<bool?>(projection, "IsDeleted") != true &&
                (!_facilityScope.IsEnforced ||
                _facilityScope.IsCrossFacility ||
                _facilityScope.FacilityIds.Contains(projection.FacilityId!)));

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
        HisHopeDataConventions.Apply(modelBuilder);
    }
}

using Microsoft.EntityFrameworkCore;

namespace His.Hope.Infrastructure.Saga;

/// <summary>
/// EF Core DbContext for saga instance persistence.
///
/// Manages the saga_instances table used by PersistentSagaOrchestrator
/// to store execution state and by SagaRecoveryService to find stale sagas.
///
/// Designed for use by any service that needs persistent saga orchestration.
/// Tables should be in the service's own database or a shared saga database.
/// </summary>
public class SagaDbContext : DbContext
{
    public SagaDbContext(DbContextOptions<SagaDbContext> options)
        : base(options)
    {
    }

    public DbSet<SagaInstance> SagaInstances => Set<SagaInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SagaInstance>(entity =>
        {
            entity.ToTable("saga_instances");

            entity.HasKey(e => e.SagaId);

            entity.Property(e => e.SagaId)
                .HasColumnName("saga_id")
                .ValueGeneratedNever();

            entity.Property(e => e.SagaType)
                .HasColumnName("saga_type")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("Pending");

            entity.Property(e => e.StepIndex)
                .HasColumnName("step_index")
                .HasDefaultValue(0);

            entity.Property(e => e.Data)
                .HasColumnName("data")
                .HasColumnType("jsonb")
                .IsRequired();

            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message")
                .HasColumnType("text");

            entity.Property(e => e.StartedAt)
                .HasColumnName("started_at")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.CompletedAt)
                .HasColumnName("completed_at");

            entity.Property(e => e.LastHeartbeat)
                .HasColumnName("last_heartbeat")
                .HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.Status, e.StartedAt })
                .HasDatabaseName("idx_saga_status");

            // Partial index: only Running/Compensating sagas need heartbeat monitoring
            entity.HasIndex(e => e.LastHeartbeat)
                .HasDatabaseName("idx_saga_heartbeat")
                .HasFilter("[status] IN ('Running', 'Compensating')");
        });
    }
}

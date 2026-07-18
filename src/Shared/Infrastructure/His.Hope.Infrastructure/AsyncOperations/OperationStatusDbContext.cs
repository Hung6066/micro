using Microsoft.EntityFrameworkCore;

namespace His.Hope.Infrastructure.AsyncOperations;

/// <summary>
/// EF Core DbContext for the async operation tracking feature.
///
/// Manages the <c>operation_status</c> table, which stores the state of
/// long-running operations for the <c>Prefer: respond-async</c> request-reply
/// pattern.
///
/// Tables are expected to be in the service's own database — each service
/// that supports async operations should register this context via
/// <c>AddOperationStatusDbContext</c>.
/// </summary>
public class OperationStatusDbContext : DbContext
{
    public OperationStatusDbContext(DbContextOptions<OperationStatusDbContext> options)
        : base(options)
    {
    }

    public DbSet<OperationStatus> OperationStatuses => Set<OperationStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OperationStatus>(entity =>
        {
            entity.ToTable("operation_status");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.OperationType)
                .HasColumnName("operation_type")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue(OperationStatusValue.Queued);

            entity.Property(e => e.Progress)
                .HasColumnName("progress")
                .HasDefaultValue(0);

            entity.Property(e => e.RequestData)
                .HasColumnName("request_data")
                .HasColumnType("jsonb");

            entity.Property(e => e.ResultData)
                .HasColumnName("result_data")
                .HasColumnType("jsonb");

            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message")
                .HasColumnType("text");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.CompletedAt)
                .HasColumnName("completed_at");

            entity.Property(e => e.ExpiresAt)
                .HasColumnName("expires_at")
                .HasDefaultValueSql("now() + INTERVAL '24 hours'");

            // Index for efficient cleanup of expired records
            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("idx_operation_status_expires")
                .IsDescending();

            // Index for polling by status
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("idx_operation_status_status");
        });
    }
}

using Microsoft.EntityFrameworkCore;

namespace His.Hope.Infrastructure.Idempotency;

/// <summary>
/// EF Core DbContext for the idempotency feature.
///
/// Manages two tables:
///   - idempotency_keys:  Ensures POST/PUT/PATCH requests are processed exactly once.
///   - processed_events:  Tracks domain events already consumed (at-most-once delivery).
///
/// The context is designed for use by the API Gateway and event bus consumers.
/// Tables are expected to be in the gateway's shared database (or a dedicated
/// idempotency database).
/// </summary>
public class IdempotencyDbContext : DbContext
{
    public IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options)
        : base(options)
    {
    }

    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdempotencyKey>(entity =>
        {
            entity.ToTable("idempotency_keys");

            entity.HasKey(e => e.IdempotencyKeyValue);
            entity.Property(e => e.IdempotencyKeyValue)
                .HasColumnName("idempotency_key")
                .HasMaxLength(255);

            entity.Property(e => e.ServiceName)
                .HasColumnName("service_name")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Endpoint)
                .HasColumnName("endpoint")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.HttpMethod)
                .HasColumnName("http_method")
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(e => e.RequestHash)
                .HasColumnName("request_hash")
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(e => e.ResponseStatusCode)
                .HasColumnName("response_status_code");

            entity.Property(e => e.ResponseBody)
                .HasColumnName("response_body")
                .HasColumnType("jsonb");

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("Processing");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("now()");

            entity.Property(e => e.ExpiresAt)
                .HasColumnName("expires_at")
                .HasDefaultValueSql("now() + INTERVAL '24 hours'");

            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("idx_idempotency_expires")
                .IsDescending();
        });

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.ToTable("processed_events");

            entity.HasKey(e => new { e.EventId, e.Consumer });

            entity.Property(e => e.EventId)
                .HasColumnName("event_id");

            entity.Property(e => e.Consumer)
                .HasColumnName("consumer")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.ProcessedAt)
                .HasColumnName("processed_at")
                .HasDefaultValueSql("now()");
        });
    }
}

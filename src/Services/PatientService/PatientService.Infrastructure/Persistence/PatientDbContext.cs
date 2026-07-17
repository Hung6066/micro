using His.Hope.Infrastructure.Outbox;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Infrastructure.Persistence.Configurations;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.PatientService.Infrastructure.Persistence;

public class PatientDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator _mediator;

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public PatientDbContext(
        DbContextOptions<PatientDbContext> options,
        IMediator mediator)
        : base(options)
    {
        _mediator = mediator;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Explicitly apply each configuration to ensure they're loaded
        modelBuilder.ApplyConfiguration(new PatientConfiguration());
        modelBuilder.ApplyConfiguration(new MedicalConditionConfiguration());
        modelBuilder.ApplyConfiguration(new AllergyConfiguration());

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(e => e.Id).HasName("pk_outbox_messages");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Type).HasColumnName("type").HasMaxLength(500).IsRequired();
            entity.Property(e => e.Content).HasColumnName("content").IsRequired();
            entity.Property(e => e.CorrelationId).HasColumnName("correlation_id").HasMaxLength(200);
            entity.Property(e => e.CausationId).HasColumnName("causation_id").HasMaxLength(200);
            entity.Property(e => e.OccurredOn).HasColumnName("occurred_on").IsRequired();
            entity.Property(e => e.ProcessedOn).HasColumnName("processed_on");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Error).HasColumnName("error").HasMaxLength(1000);
            entity.Property(e => e.RetryCount).HasColumnName("retry_count");
            entity.Property(e => e.LastRetryOn).HasColumnName("last_retry_on");
            entity.Property(e => e.LockExpiresAt).HasColumnName("lock_expires_at");
            entity.HasIndex(e => new { e.Status, e.OccurredOn }).HasDatabaseName("ix_outbox_messages_status_occurred_on");
        });
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        var domainEvents = ChangeTracker.Entries<AggregateRoot<PatientId>>()
            .Select(e => e.Entity.DomainEvents)
            .SelectMany(e => e)
            .ToList();

        // Set CreatedAt on all added entities that have a CreatedAt property
        // (handles both Entity<PatientId> and Entity<Guid> types)
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            var createdAt = entry.Metadata.FindProperty("CreatedAt");
            if (createdAt is not null)
                entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }

        return result;
    }
}

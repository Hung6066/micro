using System.Reflection;
using His.Hope.Infrastructure.Outbox;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.PharmacyService.Infrastructure.Persistence;

public class PharmacyDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator _mediator;

    public DbSet<Medication> Medications => Set<Medication>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public PharmacyDbContext(
        DbContextOptions<PharmacyDbContext> options,
        IMediator mediator)
        : base(options)
    {
        _mediator = mediator;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(e => e.Id);
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
            entity.HasIndex(e => new { e.Status, e.OccurredOn });
        });
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        var domainEvents = ChangeTracker.Entries<AggregateRoot<MedicationId>>()
            .Select(e => e.Entity.DomainEvents)
            .SelectMany(e => e)
            .ToList();

        domainEvents.AddRange(
            ChangeTracker.Entries<AggregateRoot<PrescriptionId>>()
                .Select(e => e.Entity.DomainEvents)
                .SelectMany(e => e));

        // Set CreatedAt on all added entities that have a CreatedAt property
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

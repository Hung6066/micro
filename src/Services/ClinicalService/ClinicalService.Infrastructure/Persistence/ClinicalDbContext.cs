using System.Reflection;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.Entities;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.Infrastructure.Outbox;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.ClinicalService.Infrastructure.Persistence;

public class ClinicalDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator? _mediator;

    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<ClinicalNote> ClinicalNotes => Set<ClinicalNote>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public ClinicalDbContext(
        DbContextOptions<ClinicalDbContext> options,
        IMediator? mediator = null)
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
            entity.Property(e => e.Id);
            entity.Property(e => e.Type).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(200);
            entity.Property(e => e.CausationId).HasMaxLength(200);
            entity.Property(e => e.OccurredOn).IsRequired();
            entity.Property(e => e.ProcessedOn);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Error).HasMaxLength(1000);
            entity.Property(e => e.RetryCount);
            entity.Property(e => e.LastRetryOn);
            entity.Property(e => e.LockExpiresAt);
            entity.HasIndex(e => new { e.Status, e.OccurredOn }).HasDatabaseName("ix_outboxmessages_status_occurredon");
        });
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        var domainEvents = ChangeTracker.Entries<AggregateRoot<EncounterId>>()
            .Select(e => e.Entity.DomainEvents)
            .SelectMany(e => e)
            .ToList();

        // Set CreatedAt on all added entities that have a CreatedAt property
        // (handles both Entity<EncounterId> and any other entity types)
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            var createdAt = entry.Metadata.FindProperty("CreatedAt");
            if (createdAt is not null)
                entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        if (_mediator is not null)
        {
            foreach (var domainEvent in domainEvents)
            {
                await _mediator.Publish(domainEvent, cancellationToken);
            }
        }

        return result;
    }
}

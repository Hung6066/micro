using System.Reflection;
using His.Hope.Infrastructure.Outbox;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
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
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(200);
            entity.Property(e => e.CausationId).HasMaxLength(200);
            entity.Property(e => e.OccurredOn).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Error).HasMaxLength(1000);
            entity.Property(e => e.LockExpiresAt);
            entity.HasIndex(e => new { e.Status, e.OccurredOn });
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
